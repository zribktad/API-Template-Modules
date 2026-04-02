using System.Security.Claims;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Cache;

/// <summary>
/// Output cache policy that enables caching for authenticated requests and varies the cache key
/// by tenant, preventing cross-tenant data exposure.
/// </summary>
/// <remarks>
/// By default ASP.NET Core Output Cache skips caching when an <c>Authorization</c> header is present.
/// This policy overrides that behaviour and segments the cache per tenant so one tenant's responses
/// are never served to another.
/// </remarks>
public sealed class TenantAwareOutputCachePolicy : IOutputCachePolicy
{
    /// <summary>
    /// Enables output caching for GET/HEAD requests and segments the cache key by tenant ID.
    /// Non-GET/HEAD requests are skipped without side-effects.
    /// </summary>
    public ValueTask CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            !HttpMethods.IsGet(context.HttpContext.Request.Method)
            && !HttpMethods.IsHead(context.HttpContext.Request.Method)
        )
            return ValueTask.CompletedTask;

        // Explicitly enable caching even when an Authorization header is present.
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;

        // Vary cache key by tenant so each tenant has isolated cache entries.
        var tenantId =
            context.HttpContext.User.FindFirstValue(AuthConstants.Claims.TenantId) ?? string.Empty;
        context.CacheVaryByRules.VaryByValues[AuthConstants.Claims.TenantId] = tenantId;
        CacheTelemetry.ConfigureRequest(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>Records a cache-hit telemetry event when a cached response is served.</summary>
    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        CacheTelemetry.RecordCacheHit(context);
        return ValueTask.CompletedTask;
    }

    /// <summary>Records the response outcome telemetry when a fresh response is written to the cache.</summary>
    public ValueTask ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        CacheTelemetry.RecordResponseOutcome(context);
        return ValueTask.CompletedTask;
    }
}
