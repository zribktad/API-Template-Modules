using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Options.Http;
using SharedKernel.Contracts.Events;
using SharedKernel.Infrastructure.OutputCache;

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
