using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using SharedKernel.Infrastructure.Health;
using StackExchange.Redis;
using IdentityCacheTags = Identity.Application.Events.CacheTags;
using ProductCatalogCacheTags = ProductCatalog.Application.Events.CacheTags;
using ReviewsCacheTags = Reviews.Application.Events.CacheTags;

namespace APITemplate.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);
        services.AddExceptionHandler<ApiExceptionHandler>();
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
        DragonflyOptions dragonflyOptions =
            configuration.SectionFor<DragonflyOptions>().Get<DragonflyOptions>() ?? new();

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
        {
            services.AddDistributedMemoryCache();
        }

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
            configuration.SectionFor<CachingOptions>().Get<CachingOptions>() ?? new();

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
