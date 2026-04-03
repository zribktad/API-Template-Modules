using System.Security.Claims;
using Identity.Common.Security;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Cache;

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
            context.HttpContext.User.FindFirstValue(AuthConstants.Claims.TenantId) ?? string.Empty;

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
        context.CacheVaryByRules.VaryByValues[AuthConstants.Claims.TenantId] = tenantId;

        List<string> originalTags = context.Tags.ToList();
        context.Tags.Clear();
        foreach (string tag in originalTags)
        {
            context.Tags.Add($"{tag}-{tenantId}");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;
}
