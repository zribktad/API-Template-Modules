using System.Threading.RateLimiting;
using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.Filters.Idempotency;
using APITemplate.Api.Filters.Webhooks;
using APITemplate.Api.OpenApi;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Http;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers core API services (controllers, OpenAPI, ProblemDetails) and
    /// exception handling dependencies.
    /// </summary>
    /// <remarks>
    /// This method only registers exception handling services in DI
    /// (including <see cref="ApiExceptionHandler"/>). Runtime exception interception
    /// is activated later by calling <c>app.UseExceptionHandler()</c> in the middleware pipeline.
    /// </remarks>
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Controllers are the foundation of the Web API pipeline and must be registered first.
        services.AddControllers(options =>
        {
            options.Filters.AddService<IdempotencyActionFilter>();
            options.Filters.AddService<WebhookSignatureResourceFilter>();
        });

        services.AddScoped<IdempotencyActionFilter>();
        services.AddScoped<WebhookSignatureResourceFilter>();
        services.AddIdempotencyStore();

        services
            // Register the exception / ProblemDetails handling infrastructure (RFC 7807 error payloads).
            .AddProblemDetailsAndExceptionHandling()
            // Register OpenAPI/Swagger generation and documentation transformers.
            .AddOpenApiDocumentation()
            // Configure per-client rate limiting to protect against abuse.
            .AddRateLimiting(configuration)
            // Configure output cache storage + data protection (DragonFly/Redis or in-memory fallback).
            .AddDragonflyAndDataProtection(configuration)
            // Configure output caching policies for controller endpoints.
            .AddOutputCaching(configuration);

        return services;
    }

    private static IServiceCollection AddProblemDetailsAndExceptionHandling(
        this IServiceCollection services
    )
    {
        // Registers ProblemDetails support (RFC 7807) so errors are returned as structured JSON.
        // Configure mapping from exceptions to ProblemDetails types via ApiProblemDetailsOptions.
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);

        // Registers the handler in DI; middleware activation happens in UseApiPipeline via app.UseExceptionHandler().
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    private static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
    }

    private static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RateLimitingOptions>(configuration);

        // Per-client fixed window rate limiter. Partition key priority:
        //   1. JWT username (authenticated users)
        //   2. Remote IP address (anonymous users)
        //   3. "anonymous" fallback (shared bucket when neither is available)
        // IConfigureOptions is used so values are resolved from DI at first request,
        // not captured at registration time — this allows tests to override RateLimitingOptions.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var endpoint = HttpRouteResolver.Resolve(context.HttpContext);
                ApiMetrics.RecordRateLimitRejection(
                    RateLimitPolicies.Fixed,
                    context.HttpContext.Request.Method,
                    endpoint
                );
                return ValueTask.CompletedTask;
            };
        });

        services.AddSingleton<IConfigureOptions<RateLimiterOptions>>(sp =>
        {
            var rateLimitOpts = sp.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
            return new ConfigureOptions<RateLimiterOptions>(o =>
                o.AddPolicy(
                    RateLimitPolicies.Fixed,
                    httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: httpContext.User.Identity?.Name
                                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                                ?? "anonymous",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = rateLimitOpts.PermitLimit,
                                Window = TimeSpan.FromMinutes(rateLimitOpts.WindowMinutes),
                            }
                        )
                )
            );
        });

        return services;
    }

    private static IServiceCollection AddDragonflyAndDataProtection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Output Cache with optional DragonFly backing store.
        // When Dragonfly:ConnectionString is configured, cached responses are stored in DragonFly
        // so all application instances share the same cache. Without it, falls back to in-memory.
        // Each policy defines an expiration time and a tag used for targeted invalidation
        // via IOutputCacheStore.EvictByTagAsync() in controllers after mutations (Create/Update/Delete).
        var dragonflySection = configuration.SectionFor<DragonflyOptions>();
        var dragonflyConnectionString = dragonflySection.GetValue<string>(
            nameof(DragonflyOptions.ConnectionString)
        );

        if (!string.IsNullOrEmpty(dragonflyConnectionString))
        {
            services.AddValidatedOptions<DragonflyOptions>(configuration);

            var connectTimeoutMs = dragonflySection.GetValue(
                nameof(DragonflyOptions.ConnectTimeoutMs),
                DragonflyOptions.DefaultConnectTimeoutMs
            );
            var syncTimeoutMs = dragonflySection.GetValue(
                nameof(DragonflyOptions.SyncTimeoutMs),
                DragonflyOptions.DefaultSyncTimeoutMs
            );

            var configOptions = ConfigurationOptions.Parse(dragonflyConnectionString);
            configOptions.ConnectTimeout = connectTimeoutMs;
            configOptions.SyncTimeout = syncTimeoutMs;
            configOptions.AbortOnConnectFail = false;

            // Lazy singleton: the TCP connection is established on first use, not at registration time.
            // This keeps startup fast and allows tests to replace Redis services before the
            // connection is ever attempted.
            var lazyMultiplexer = new Lazy<IConnectionMultiplexer>(() =>
                ConnectionMultiplexer.Connect(configOptions)
            );
            services.AddSingleton(_ => lazyMultiplexer.Value);

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(lazyMultiplexer.Value);
                options.InstanceName = RedisInstanceNames.OutputCache;
            });

            services
                .AddDataProtection()
                .PersistKeysToStackExchangeRedis(
                    () => lazyMultiplexer.Value.GetDatabase(),
                    "DataProtection:Keys"
                );

            services.AddSingleton<IConfigureOptions<DataProtectionOptions>>(sp =>
            {
                var appOptions = sp.GetRequiredService<IOptions<AppOptions>>().Value;
                return new ConfigureOptions<DataProtectionOptions>(o =>
                    o.ApplicationDiscriminator = appOptions.ServiceName
                );
            });

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(lazyMultiplexer.Value);
                options.InstanceName = RedisInstanceNames.Session;
            });

            services
                .AddHealthChecks()
                .AddRedis(
                    dragonflyConnectionString,
                    name: HealthCheckNames.Dragonfly,
                    tags: ["cache"]
                );
        }
        else
        {
            Log.Warning(
                "Dragonfly:ConnectionString is not configured — using in-memory output cache. "
                    + "This is not suitable for multi-instance deployments"
            );
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    private static IServiceCollection AddOutputCaching(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Tenant-aware policy varies cache entries by tenant to prevent cross-tenant data leaks.
        services.AddSingleton<TenantAwareOutputCachePolicy>();

        // Tag-based invalidation service used by domain event handlers to evict stale entries.
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        // Bind expiration settings from "Caching" section with startup validation.
        services.AddValidatedOptions<CachingOptions>(configuration);

        services.AddOutputCache();

        // Deferred configuration — resolves CachingOptions from DI at first use,
        // same pattern as AddRateLimiting above.
        services.AddSingleton<
            IConfigureOptions<Microsoft.AspNetCore.OutputCaching.OutputCacheOptions>
        >(sp =>
        {
            var cachingOptions = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
            return new ConfigureOptions<Microsoft.AspNetCore.OutputCaching.OutputCacheOptions>(
                options =>
                {
                    // No caching by default — only endpoints with explicit [OutputCache] attributes are cached.
                    options.AddBasePolicy(builder => builder.NoCache());

                    // Each named policy uses tenant-aware isolation, a configurable expiration,
                    // and a tag matching the policy name for targeted invalidation.
                    ReadOnlySpan<(string Name, int ExpirationSeconds)> policies =
                    [
                        (CacheTags.Products, cachingOptions.ProductsExpirationSeconds),
                        (CacheTags.Categories, cachingOptions.CategoriesExpirationSeconds),
                        (CacheTags.Reviews, cachingOptions.ReviewsExpirationSeconds),
                        (CacheTags.ProductData, cachingOptions.ProductDataExpirationSeconds),
                        (CacheTags.Tenants, cachingOptions.TenantsExpirationSeconds),
                        (
                            CacheTags.TenantInvitations,
                            cachingOptions.TenantInvitationsExpirationSeconds
                        ),
                        (CacheTags.Users, cachingOptions.UsersExpirationSeconds),
                    ];

                    foreach (var (name, expirationSeconds) in policies)
                    {
                        options.AddPolicy(
                            name,
                            builder =>
                                builder
                                    .AddPolicy<TenantAwareOutputCachePolicy>()
                                    .Expire(TimeSpan.FromSeconds(expirationSeconds))
                                    .Tag(name)
                        );
                    }
                }
            );
        });

        return services;
    }
}
