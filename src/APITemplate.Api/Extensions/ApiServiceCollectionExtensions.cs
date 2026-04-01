using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using ProductCatalogCacheTags = ProductCatalog.Application.Events.CacheTags;
using ReviewsCacheTags = Reviews.Application.Events.CacheTags;
using SharedKernel.Infrastructure.Configuration;

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
        services.AddHealthChecks();
        services.AddCaching(configuration);
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
        });

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
