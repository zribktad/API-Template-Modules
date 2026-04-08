using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using SharedKernel.Application.Options.Http;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Api.Routing;
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
        services.ConfigureOptions<ProblemDetailsErrorTypeConfigureOptions>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseRouteTokenTransformer())
            );
        });
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

        services.AddOutputCache(options => options.AddBasePolicy(builder => builder.NoCache()));

        return services;
    }
}
