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
        options.AddPolicy(
            CacheTags.Reviews,
            builder =>
                builder
                    .AddPolicy<TenantAwareOutputCachePolicy>()
                    .Expire(TimeSpan.FromSeconds(o.ReviewsExpirationSeconds))
                    .Tag(CacheTags.Reviews)
        );
    }
}
