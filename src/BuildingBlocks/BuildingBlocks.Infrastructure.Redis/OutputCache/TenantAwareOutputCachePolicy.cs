using System.Security.Claims;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace BuildingBlocks.Infrastructure.Redis.OutputCache;

public sealed class TenantAwareOutputCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            !HttpMethods.IsGet(context.HttpContext.Request.Method)
            && !HttpMethods.IsHead(context.HttpContext.Request.Method)
        )
        {
            return ValueTask.CompletedTask;
        }

        string tenantId =
            context.HttpContext.User.FindFirstValue(TenantSecurityClaims.TenantId) ?? string.Empty;

        if (string.IsNullOrEmpty(tenantId))
        {
            context.EnableOutputCaching = false;
            context.AllowCacheLookup = false;
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.CacheVaryByRules.VaryByValues[TenantSecurityClaims.TenantId] = tenantId;

        List<string> originalTags = context.Tags.ToList();
        context.Tags.Clear();
        foreach (string tag in originalTags)
            context.Tags.Add($"{tag}-{tenantId}");

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.CompletedTask;
    }
}
