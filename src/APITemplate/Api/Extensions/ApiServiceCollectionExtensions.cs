using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using APITemplate.Api.Security;
using Asp.Versioning;
using ErrorOr;
using Identity.Auth.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OutputCaching.StackExchangeRedis;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using SharedKernel.Application.Context;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Options.Http;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Application.Validation;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Api.Routing;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.OutputCache;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        AddRequestContext(services);
        AddApiVersioning(services);
        AddRequestValidation(services);
        AddErrorHandling(services, configuration);
        AddMvcConventions(services);

        ConfigurationOptions? redisConfiguration = null;
        if (configuration.IsRedisConfigured())
            redisConfiguration = BuildRedisConfigurationOptions(configuration);

        services.AddRedisInfrastructure(configuration, redisConfiguration);
        services.AddCaching(configuration, redisConfiguration);
        services.AddOpenApiDocumentation();

        return services;
    }

    // Registers IHttpContextAccessor and the single per-request adapter that exposes the
    // authenticated user's identity and tenant to the application layer.
    private static void AddRequestContext(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<HttpRequestIdentityProvider>();
        services.AddScoped<ICurrentRequestUser>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
        services.AddScoped<IActorProvider>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
        services.AddScoped<ITenantProvider>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
    }

    private static void AddApiVersioning(IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
    }

    // Registers data-annotation-based validators and the ASP.NET Core validation pipeline.
    private static void AddRequestValidation(IServiceCollection services)
    {
        services.AddSingleton<IValidator, DataAnnotationsValidator>();
        services.AddScoped(typeof(IBatchRule<>), typeof(DataAnnotationsBatchRule<>));
        Microsoft.Extensions.DependencyInjection.ValidationServiceCollectionExtensions.AddValidation(
            services
        );
    }

    // Registers RFC 7807 ProblemDetails, the global exception handler, and error metrics.
    private static void AddErrorHandling(IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<ApiExceptionMetrics>();
    }

    // Applies kebab-case URL routing and maps model validation errors to ProblemDetails.
    private static void AddMvcConventions(IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseRouteTokenTransformer())
            );
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = BuildModelStateErrorResponse;
        });
    }

    private static IActionResult BuildModelStateErrorResponse(ActionContext context)
    {
        List<Error> errors = context
            .ModelState.Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .SelectMany(kvp =>
                kvp.Value!.Errors.Select(error =>
                {
                    Dictionary<string, object> metadata = [];
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                        metadata["propertyName"] = kvp.Key;

                    return Error.Validation(
                        ErrorCatalog.General.ValidationFailed,
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The request is invalid."
                            : error.ErrorMessage,
                        metadata.Count > 0 ? metadata : null
                    );
                })
            )
            .ToList();

        if (errors.Count == 0)
            errors.Add(
                Error.Validation(ErrorCatalog.General.ValidationFailed, "The request is invalid.")
            );

        ProblemDetails problemDetails = errors.ToProblemDetails(context.HttpContext);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }

    private static void AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });
    }

    private static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ConfigurationOptions? redisConfiguration
    )
    {
        services.AddValidatedOptions<RedisOptions>(configuration);

        if (redisConfiguration is not null)
        {
            IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(_ => redis);

            services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConfigurationOptions = redisConfiguration;
            });

            services
                .AddDataProtection()
                .SetApplicationName("APITemplate")
                .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    // StackExchange.Redis connection settings shared by distributed cache, output cache, and IConnectionMultiplexer.
    private static ConfigurationOptions BuildRedisConfigurationOptions(IConfiguration configuration)
    {
        RedisOptions redisOptions =
            configuration.SectionFor<RedisOptions>().Get<RedisOptions>() ?? new RedisOptions();
        ConfigurationOptions redisConfig = ConfigurationOptions.Parse(
            redisOptions.ConnectionString
        );
        redisConfig.ConnectTimeout = redisOptions.ConnectTimeoutMs;
        redisConfig.SyncTimeout = redisOptions.SyncTimeoutMs;
        redisConfig.AbortOnConnectFail = false;
        return redisConfig;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        ConfigurationOptions? redisConfiguration
    )
    {
        services.AddValidatedOptions<CachingOptions>(configuration);
        services.AddSingleton<TenantAwareOutputCachePolicy>();
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        services.AddOutputCache(options => options.AddBasePolicy(builder => builder.NoCache()));

        if (redisConfiguration is not null)
        {
            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.ConfigurationOptions = redisConfiguration;
                options.InstanceName = RedisInstanceNames.OutputCache;
            });
        }

        return services;
    }
}
