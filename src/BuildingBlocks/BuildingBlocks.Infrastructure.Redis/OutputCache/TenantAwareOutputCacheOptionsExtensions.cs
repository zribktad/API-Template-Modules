using System;
using Microsoft.AspNetCore.OutputCaching;

namespace BuildingBlocks.Infrastructure.Redis.OutputCache;

public static class TenantAwareOutputCacheOptionsExtensions
{
    public static void AddTenantAwareTaggedPolicy(
        this OutputCacheOptions options,
        string name,
        int expirationSeconds
    )
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

