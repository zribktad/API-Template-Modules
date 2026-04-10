using Identity.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;

namespace Identity.Security.Sessions;

/// <summary>
///     Cookie authentication event handler that validates the current opaque session cookie against
///     the server-side BFF session store and coordinates silent refresh when the access token is
///     near expiry.
/// </summary>
public sealed class CookieSessionRefresher : CookieAuthenticationEvents
{
    private readonly IBffSessionPrincipalFactory _principalFactory;
    private readonly IBffTokenRefreshService _refreshService;
    private readonly ILogger<CookieSessionRefresher> _logger;

    public CookieSessionRefresher(
        IBffSessionPrincipalFactory principalFactory,
        IBffTokenRefreshService refreshService,
        ILogger<CookieSessionRefresher> logger
    )
    {
        _principalFactory = principalFactory;
        _refreshService = refreshService;
        _logger = logger;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? sessionId = ResolveSessionId(context);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            context.RejectPrincipal();
            return;
        }

        BffRefreshOutcome refreshOutcome = await _refreshService.RefreshIfRequiredAsync(
            sessionId,
            context.HttpContext.RequestAborted
        );

        if (!refreshOutcome.Succeeded || refreshOutcome.Session is null)
        {
            _logger.BffSessionRefreshFailedRejectingPrincipal();
            context.RejectPrincipal();
            return;
        }

        if (!refreshOutcome.RequiresRenewal)
            return;

        context.Principal = _principalFactory.CreatePrincipal(refreshOutcome.Session);
        context.HttpContext.User = context.Principal;

        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.AccessToken,
            refreshOutcome.Session.AccessToken
        );
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.RefreshToken,
            refreshOutcome.Session.RefreshToken
        );
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.ExpiresAt,
            refreshOutcome.Session.AccessTokenExpiresAtUtc.ToString("o")
        );
        context.ShouldRenew = true;
    }

    private static string? ResolveSessionId(CookieValidatePrincipalContext context)
    {
        if (
            context.Properties.Items.TryGetValue(
                AuthConstants.SessionProperties.SessionId,
                out string? sessionId
            ) && !string.IsNullOrWhiteSpace(sessionId)
        )
        {
            return sessionId;
        }

        return null;
    }
}
