using System.Net.Http.Json;
using Identity.Options;
using Identity.Security.Keycloak;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security;

/// <summary>
/// Provides the cookie authentication principal validation callback used to transparently
/// refresh Keycloak-backed BFF sessions when access tokens are close to expiration.
/// </summary>
public static class CookieSessionRefresher
{
    /// <summary>
    /// Validates an incoming cookie principal and, when appropriate, attempts to refresh
    /// the underlying Keycloak session and update the authentication cookie.
    /// </summary>
    public static async Task OnValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!TryCreateRefreshRequest(context, out RefreshRequest refreshRequest))
            return;

        KeycloakTokenResponse? tokenResponse = await TryRefreshSessionAsync(context, refreshRequest);
        if (tokenResponse is null)
        {
            GetLogger(context).LogWarning("BFF session refresh failed — rejecting principal.");
            context.RejectPrincipal();
            return;
        }

        ApplyRefreshedSession(context, tokenResponse, refreshRequest.RefreshToken);
    }

    private static bool TryCreateRefreshRequest(
        CookieValidatePrincipalContext context,
        out RefreshRequest refreshRequest
    )
    {
        refreshRequest = default;

        if (!TryGetExpiration(context, out DateTimeOffset expiresAt))
            return false;

        if (!IsRefreshRequired(context, expiresAt))
            return false;

        if (!TryGetRefreshToken(context, out string refreshToken))
        {
            GetLogger(context).LogWarning("BFF session refresh skipped — no refresh token found.");
            context.RejectPrincipal();
            return false;
        }

        refreshRequest = new RefreshRequest(GetKeycloakOptions(context), refreshToken);
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

    private static bool IsRefreshRequired(
        CookieValidatePrincipalContext context,
        DateTimeOffset expiresAt
    )
    {
        BffOptions bffOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<BffOptions>>()
            .Value;

        return expiresAt - DateTimeOffset.UtcNow
            <= TimeSpan.FromMinutes(bffOptions.TokenRefreshThresholdMinutes);
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

    private static async Task<KeycloakTokenResponse?> TryRefreshSessionAsync(
        CookieValidatePrincipalContext context,
        RefreshRequest refreshRequest
    )
    {
        string tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            refreshRequest.KeycloakOptions.AuthServerUrl,
            refreshRequest.KeycloakOptions.Realm
        );

        HttpClient client = context.HttpContext.RequestServices
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(AuthConstants.HttpClients.KeycloakToken);

        try
        {
            using HttpResponseMessage response = await client.PostAsync(
                tokenEndpoint,
                BuildRefreshContent(refreshRequest.KeycloakOptions, refreshRequest.RefreshToken),
                context.HttpContext.RequestAborted
            );

            if (!response.IsSuccessStatusCode)
            {
                GetLogger(context).LogWarning(
                    "Keycloak token endpoint returned {StatusCode} during BFF refresh.",
                    (int)response.StatusCode
                );
                return null;
            }

            return await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(
                context.HttpContext.RequestAborted
            );
        }
        catch (Exception ex)
        {
            GetLogger(context).LogWarning(ex, "Token refresh failed, rejecting principal.");
            return null;
        }
    }

    private static FormUrlEncodedContent BuildRefreshContent(
        KeycloakOptions keycloakOptions,
        string refreshToken
    )
    {
        Dictionary<string, string> formParams = new()
        {
            [AuthConstants.OAuth2FormParameters.GrantType] =
                AuthConstants.OAuth2GrantTypes.RefreshToken,
            [AuthConstants.OAuth2FormParameters.ClientId] = keycloakOptions.Resource,
            [AuthConstants.OAuth2FormParameters.RefreshToken] = refreshToken,
        };

        if (!string.IsNullOrEmpty(keycloakOptions.Credentials.Secret))
            formParams[AuthConstants.OAuth2FormParameters.ClientSecret] =
                keycloakOptions.Credentials.Secret;

        return new FormUrlEncodedContent(formParams);
    }

    private static void ApplyRefreshedSession(
        CookieValidatePrincipalContext context,
        KeycloakTokenResponse tokenResponse,
        string refreshToken
    )
    {
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
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o")
        );
        context.ShouldRenew = true;
    }

    private static KeycloakOptions GetKeycloakOptions(CookieValidatePrincipalContext context) =>
        context.HttpContext.RequestServices
            .GetRequiredService<IOptions<KeycloakOptions>>()
            .Value;

    private static ILogger GetLogger(CookieValidatePrincipalContext context) =>
        context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(CookieSessionRefresher));

    private readonly record struct RefreshRequest(KeycloakOptions KeycloakOptions, string RefreshToken);
}

