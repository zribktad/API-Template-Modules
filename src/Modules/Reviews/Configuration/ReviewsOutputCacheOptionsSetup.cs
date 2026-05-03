using BuildingBlocks.Application.Options.Http;
using BuildingBlocks.Infrastructure.Redis.OutputCache;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Events;

namespace Reviews.Configuration;

internal sealed class ReviewsOutputCacheOptionsSetup(IOptions<CachingOptions> cachingOptions)
    : IConfigureOptions<OutputCacheOptions>
{
    public void Configure(OutputCacheOptions options)
    {
        CachingOptions o = cachingOptions.Value;
        options.AddTenantAwareTaggedPolicy(CacheTags.Reviews, o.ReviewsExpirationSeconds);
    }
}
