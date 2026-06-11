using System.Security.Claims;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace BuildingBlocks.Infrastructure.Redis.OutputCache;

/// <summary>
///     Output-cache policy that re-enables caching for authenticated responses and varies the cache entry
///     <b>only by tenant</b>.
///     <para>
///         ⚠ INVARIANT: only decorate endpoints whose response is <b>uniform across all users within a
///         tenant</b> (e.g. the shared product catalog). An endpoint that returns per-user data
///         (RBAC-filtered fields, "my …" semantics) MUST NOT use this policy — its first caller's body would
///         be served to every other user in the tenant until the TTL expires. For per-user responses, add a
///         user discriminator to <see cref="OutputCacheContext.CacheVaryByRules" /> or do not cache at all.
///     </para>
/// </summary>
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
