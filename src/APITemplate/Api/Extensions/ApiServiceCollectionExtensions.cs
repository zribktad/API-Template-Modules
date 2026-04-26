using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using ErrorOr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OutputCaching.StackExchangeRedis;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
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
        services.AddSingleton<IValidator, DataAnnotationsValidator>();
        services.AddScoped(typeof(IBatchRule<>), typeof(DataAnnotationsBatchRule<>));
        Microsoft.Extensions.DependencyInjection.ValidationServiceCollectionExtensions.AddValidation(
            services
        );
        services.ConfigureOptions<ProblemDetailsErrorTypeConfigureOptions>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSingleton<ApiExceptionMetrics>();
        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseRouteTokenTransformer())
            );
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
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
                {
                    errors.Add(
                        Error.Validation(
                            ErrorCatalog.General.ValidationFailed,
                            "The request is invalid."
                        )
                    );
                }

                ProblemDetails problemDetails = errors.ToProblemDetails(context.HttpContext);
                return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
            };
        });

        ConfigurationOptions? redisConfiguration = null;
        if (configuration.IsRedisConfigured())
            redisConfiguration = BuildRedisConfigurationOptions(configuration);

        services.AddRedisInfrastructure(configuration, redisConfiguration);
        services.AddCaching(configuration, redisConfiguration);
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
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

    /// <summary>
    ///     StackExchange.Redis connection settings shared by distributed cache, output cache, and
    ///     <see cref="IConnectionMultiplexer" />.
    /// </summary>
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
