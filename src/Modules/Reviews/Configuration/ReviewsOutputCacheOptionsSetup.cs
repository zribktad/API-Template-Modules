using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using Reviews.Common.Events;
using SharedKernel.Application.Options.Http;
using SharedKernel.Infrastructure.OutputCache;

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
