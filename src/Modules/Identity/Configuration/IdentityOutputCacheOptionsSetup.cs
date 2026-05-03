using BuildingBlocks.Application.Options.Http;
using BuildingBlocks.Infrastructure.Redis.OutputCache;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Events;

namespace Identity.Configuration;

internal sealed class IdentityOutputCacheOptionsSetup(IOptions<CachingOptions> cachingOptions)
    : IConfigureOptions<OutputCacheOptions>
{
    public void Configure(OutputCacheOptions options)
    {
        CachingOptions o = cachingOptions.Value;
        options.AddTenantAwareTaggedPolicy(CacheTags.Tenants, o.TenantsExpirationSeconds);
        options.AddTenantAwareTaggedPolicy(
            CacheTags.TenantInvitations,
            o.TenantInvitationsExpirationSeconds
        );
        options.AddTenantAwareTaggedPolicy(CacheTags.Users, o.UsersExpirationSeconds);
    }
}
