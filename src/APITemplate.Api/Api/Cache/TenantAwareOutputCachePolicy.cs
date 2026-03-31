using System.Security.Claims;
using Identity.Application.Common.Security;
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

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;

        string tenantId =
            context.HttpContext.User.FindFirstValue(AuthConstants.Claims.TenantId) ?? string.Empty;
        context.CacheVaryByRules.VaryByValues[AuthConstants.Claims.TenantId] = tenantId;

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
