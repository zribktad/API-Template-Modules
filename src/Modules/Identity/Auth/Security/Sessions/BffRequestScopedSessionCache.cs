using Microsoft.AspNetCore.Http;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Deduplicates <see cref="BffSessionService.GetSessionAsync" /> within a single HTTP request
///     (e.g. cookie middleware loads the ticket, then <see cref="CookieSessionRefresher" /> loads again).
///     Invalidated whenever the session row is mutated so refresh / revoke paths never see stale data.
/// </summary>
internal static class BffRequestScopedSessionCache
{
    private const string ItemKeyPrefix = "Identity.BffSession.RequestCache:";

    public static string GetItemKey(string sessionId) => ItemKeyPrefix + sessionId;

    public static void Invalidate(IHttpContextAccessor httpContextAccessor, string sessionId)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        httpContext.Items.Remove(GetItemKey(sessionId));
    }
}
