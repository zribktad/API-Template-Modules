using Identity.Logging;
using Identity.Options;
using Identity.Security.Keycloak;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security;

/// <summary>
///     Cookie authentication event handler that transparently refreshes Keycloak-backed BFF
///     sessions when access tokens are close to expiration.
/// </summary>
public sealed class CookieSessionRefresher : CookieAuthenticationEvents
{
    private readonly BffOptions _bffOptions;
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<CookieSessionRefresher> _logger;
    private readonly TimeProvider _timeProvider;

    public CookieSessionRefresher(
        IOptions<BffOptions> bffOptions,
        IKeycloakService keycloakService,
        ILogger<CookieSessionRefresher> logger,
        TimeProvider timeProvider
    )
    {
        _bffOptions = bffOptions.Value;
        _keycloakService = keycloakService;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    ///     Validates an incoming cookie principal and, when appropriate, refreshes the underlying
    ///     Keycloak session and renews the authentication cookie.
    /// </summary>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!TryCreateRefreshRequest(context, out RefreshRequest refreshRequest))
            return;

        KeycloakTokenResponse? tokenResponse = await TryRefreshSessionAsync(context, refreshRequest);
        if (tokenResponse is null)
        {
            _logger.BffSessionRefreshFailedRejectingPrincipal();
            context.RejectPrincipal();
            return;
        }

        ApplyRefreshedSession(context, tokenResponse, refreshRequest.RefreshToken);
    }

    private bool TryCreateRefreshRequest(
        CookieValidatePrincipalContext context,
        out RefreshRequest refreshRequest
    )
    {
        refreshRequest = default;

        // Missing or malformed token metadata means the cookie session is no longer trustworthy.
        if (!TryGetExpiration(context, out DateTimeOffset expiresAt))
        {
            _logger.BffSessionRefreshInvalidExpiresAtRejectingPrincipal();
            context.RejectPrincipal();
            return false;
        }

        if (!IsRefreshRequired(expiresAt))
            return false;

        // Once the access token is near expiry, a refresh token is required to keep the session alive.
        if (!TryGetRefreshToken(context, out string refreshToken))
        {
            _logger.BffSessionRefreshSkippedNoRefreshToken();
            context.RejectPrincipal();
            return false;
        }

        refreshRequest = new RefreshRequest(refreshToken);
        return true;
    }

    private static bool TryGetExpiration(
        CookieValidatePrincipalContext context,
        out DateTimeOffset expiresAt
    )
    {
        expiresAt = default;
        string? expiresAtStr = context.Properties.GetTokenValue(
            AuthConstants.CookieTokenNames.ExpiresAt
        );
        return expiresAtStr is not null && DateTimeOffset.TryParse(expiresAtStr, out expiresAt);
    }

    private bool IsRefreshRequired(DateTimeOffset expiresAt)
    {
        return expiresAt - _timeProvider.GetUtcNow()
            <= TimeSpan.FromMinutes(_bffOptions.TokenRefreshThresholdMinutes);
    }

    private static bool TryGetRefreshToken(
        CookieValidatePrincipalContext context,
        out string refreshToken
    )
    {
        refreshToken =
            context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken)
            ?? string.Empty;
        return !string.IsNullOrEmpty(refreshToken);
    }

    private async Task<KeycloakTokenResponse?> TryRefreshSessionAsync(
        CookieValidatePrincipalContext context,
        RefreshRequest refreshRequest
    )
    {
        try
        {
            // Delegate the actual token-endpoint call to the Keycloak service so this event handler
            // stays focused on cookie/session orchestration.
            return await _keycloakService.RefreshSessionAsync(
                refreshRequest.RefreshToken,
                context.HttpContext.RequestAborted
            );
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // KeycloakService already logs the failure reason; return null so the caller can reject
            // the principal without duplicating warning logs.
            return null;
        }
    }

    private void ApplyRefreshedSession(
        CookieValidatePrincipalContext context,
        KeycloakTokenResponse tokenResponse,
        string refreshToken
    )
    {
        // Persist the freshly issued tokens back into the auth ticket and ask ASP.NET Core
        // to re-issue the cookie with the updated session payload.
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.AccessToken,
            tokenResponse.AccessToken
        );
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.RefreshToken,
            tokenResponse.RefreshToken ?? refreshToken
        );
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.ExpiresAt,
            _timeProvider.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn).ToString("o")
        );
        context.ShouldRenew = true;
    }
    private readonly record struct RefreshRequest(string RefreshToken);
}
