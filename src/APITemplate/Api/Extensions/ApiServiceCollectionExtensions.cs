using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using SharedKernel.Contracts.Api.Routing;
using SharedKernel.Infrastructure.Health;
using StackExchange.Redis;
using IdentityCacheTags = Identity.Events.CacheTags;
using ProductCatalogCacheTags = ProductCatalog.Common.Events.CacheTags;
using ReviewsCacheTags = Reviews.Common.Events.CacheTags;

namespace APITemplate.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddValidatedOptions<ErrorDocumentationOptions>(
                configuration,
                validateDataAnnotations: false
            )
            .Validate(
                static o => ProblemDetailsErrorTypeUri.IsValidBaseUriWhenSet(o.ErrorTypeBaseUri),
                "ErrorDocumentation:ErrorTypeBaseUri must be an absolute http or https URI when set."
            );
        services.AddProblemDetails();
        services.ConfigureOptions<ProblemDetailsErrorTypeConfigureOptions>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseRouteTokenTransformer())
            );
        });
        services.AddValidatedOptions<KeycloakHealthCheckOptions>(configuration);
        services
            .AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString(ConfigurationSections.DefaultConnection)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{ConfigurationSections.DefaultConnection}' is not configured."
                    ),
                name: HealthCheckNames.PostgreSql
            )
            .AddCheck<KeycloakHealthCheck>(HealthCheckNames.Keycloak);
        services.AddDragonflyInfrastructure(configuration);
        services.AddCaching(configuration);
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
    }

    private static IServiceCollection AddDragonflyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<DragonflyOptions>(configuration);
        DragonflyOptions dragonflyOptions =
            configuration.SectionFor<DragonflyOptions>().Get<DragonflyOptions>()
            ?? new DragonflyOptions();

        if (!string.IsNullOrWhiteSpace(dragonflyOptions.ConnectionString))
        {
            ConfigurationOptions redisConfig = ConfigurationOptions.Parse(
                dragonflyOptions.ConnectionString
            );
            redisConfig.ConnectTimeout = dragonflyOptions.ConnectTimeoutMs;
            redisConfig.SyncTimeout = dragonflyOptions.SyncTimeoutMs;
            redisConfig.AbortOnConnectFail = false;

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConfig)
            );

            services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConfigurationOptions = redisConfig;
            });

            services
                .AddHealthChecks()
                .AddRedis(dragonflyOptions.ConnectionString, HealthCheckNames.Dragonfly);
        }
        else
            services.AddDistributedMemoryCache();

        return services;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<CachingOptions>(configuration);
        services.AddSingleton<TenantAwareOutputCachePolicy>();
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        CachingOptions cachingOptions =
            configuration.SectionFor<CachingOptions>().Get<CachingOptions>()
            ?? new CachingOptions();

        services.AddOutputCache(options =>
        {
            options.AddBasePolicy(builder => builder.NoCache());

            ReadOnlySpan<(string Name, int ExpirationSeconds)> policies =
            [
                (ProductCatalogCacheTags.Products, cachingOptions.ProductsExpirationSeconds),
                (ProductCatalogCacheTags.Categories, cachingOptions.CategoriesExpirationSeconds),
                (ReviewsCacheTags.Reviews, cachingOptions.ReviewsExpirationSeconds),
                (ProductCatalogCacheTags.ProductData, cachingOptions.ProductDataExpirationSeconds),
                (IdentityCacheTags.Tenants, cachingOptions.TenantsExpirationSeconds),
                (
                    IdentityCacheTags.TenantInvitations,
                    cachingOptions.TenantInvitationsExpirationSeconds
                ),
                (IdentityCacheTags.Users, cachingOptions.UsersExpirationSeconds),
            ];

            foreach ((string name, int expirationSeconds) in policies)
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
        });

        return services;
    }
}
