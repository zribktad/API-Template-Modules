using Identity.Events;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Options.Http;
using SharedKernel.Infrastructure.OutputCache;

namespace Identity.Configuration;

internal sealed class IdentityOutputCacheOptionsSetup(IOptions<CachingOptions> cachingOptions)
    : IConfigureOptions<OutputCacheOptions>
{
    public void Configure(OutputCacheOptions options)
    {
        CachingOptions o = cachingOptions.Value;
        AddPolicy(options, CacheTags.Tenants, o.TenantsExpirationSeconds);
        AddPolicy(options, CacheTags.TenantInvitations, o.TenantInvitationsExpirationSeconds);
        AddPolicy(options, CacheTags.Users, o.UsersExpirationSeconds);
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
