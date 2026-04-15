using Identity.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Cookie authentication event handler that validates the current opaque session cookie against
///     the server-side BFF session store and coordinates silent refresh when the access token is
///     near expiry.
/// </summary>
public sealed class CookieSessionRefresher : CookieAuthenticationEvents
{
    private readonly IBffSessionService _sessionService;
    private readonly IBffSessionPrincipalFactory _principalFactory;
    private readonly IBffTokenRefreshService _refreshService;
    private readonly ILogger<CookieSessionRefresher> _logger;

    public CookieSessionRefresher(
        IBffSessionService sessionService,
        IBffSessionPrincipalFactory principalFactory,
        IBffTokenRefreshService refreshService,
        ILogger<CookieSessionRefresher> logger
    )
    {
        _sessionService = sessionService;
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

        CancellationToken ct = context.HttpContext.RequestAborted;

        BffSessionRecord? session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
        {
            context.RejectPrincipal();
            return;
        }

        BffRefreshOutcome refreshOutcome = await _refreshService.RefreshIfRequiredAsync(
            session,
            ct
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
        context.ShouldRenew = true;

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
    }

    private static string? ResolveSessionId(CookieValidatePrincipalContext context) =>
        context.Properties.TryGetBffSessionId(out string? sessionId) ? sessionId : null;

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        ApplyStatusCodeIfNotStarted(context.Response, StatusCodes.Status401Unauthorized);

    public override Task RedirectToAccessDenied(
        RedirectContext<CookieAuthenticationOptions> context
    ) => ApplyStatusCodeIfNotStarted(context.Response, StatusCodes.Status403Forbidden);

    private static Task ApplyStatusCodeIfNotStarted(HttpResponse response, int statusCode)
    {
        if (!response.HasStarted)
            response.StatusCode = statusCode;

        return Task.CompletedTask;
    }
}
