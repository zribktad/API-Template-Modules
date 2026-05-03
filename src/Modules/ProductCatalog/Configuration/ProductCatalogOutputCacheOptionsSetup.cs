using BuildingBlocks.Application.Options.Http;
using BuildingBlocks.Infrastructure.Redis.OutputCache;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Events;

namespace ProductCatalog.Configuration;

internal sealed class ProductCatalogOutputCacheOptionsSetup(IOptions<CachingOptions> cachingOptions)
    : IConfigureOptions<OutputCacheOptions>
{
    public void Configure(OutputCacheOptions options)
    {
        CachingOptions o = cachingOptions.Value;
        options.AddTenantAwareTaggedPolicy(CacheTags.Products, o.ProductsExpirationSeconds);
        options.AddTenantAwareTaggedPolicy(CacheTags.Categories, o.CategoriesExpirationSeconds);
        options.AddTenantAwareTaggedPolicy(CacheTags.ProductData, o.ProductDataExpirationSeconds);
    }
}
