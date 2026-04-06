using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using ProductCatalog.Common.Events;
using SharedKernel.Application.Options.Http;
using SharedKernel.Infrastructure.OutputCache;

namespace ProductCatalog.Configuration;

internal sealed class ProductCatalogOutputCacheOptionsSetup(IOptions<CachingOptions> cachingOptions)
    : IConfigureOptions<OutputCacheOptions>
{
    public void Configure(OutputCacheOptions options)
    {
        CachingOptions o = cachingOptions.Value;
        AddPolicy(options, CacheTags.Products, o.ProductsExpirationSeconds);
        AddPolicy(options, CacheTags.Categories, o.CategoriesExpirationSeconds);
        AddPolicy(options, CacheTags.ProductData, o.ProductDataExpirationSeconds);
    }

    private static void AddPolicy(OutputCacheOptions options, string name, int expirationSeconds)
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
