using System.Net.Http.Json;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// Provides the cookie authentication principal validation callback used to transparently
/// refresh Keycloak-backed BFF sessions when access tokens are close to expiration.
/// </summary>
/// <remarks>
/// This type is part of the Infrastructure assembly's public surface so that authentication
/// configuration in the API layer can wire <see cref="OnValidatePrincipal"/> into
/// <see cref="CookieAuthenticationOptions.Events"/> for the BFF cookie scheme.
/// It is not intended for general-purpose use outside of authentication setup.
/// </remarks>
public static class CookieSessionRefresher
{
    /// <summary>
    /// Validates an incoming cookie principal and, when appropriate, attempts to refresh
    /// the underlying Keycloak session and update the authentication cookie.
    /// </summary>
    /// <param name="context">The cookie validation context supplied by ASP.NET Core authentication.</param>
    public static async Task OnValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!TryCreateRefreshRequest(context, out var refreshRequest))
            return;

        var tokenResponse = await TryRefreshSessionAsync(context, refreshRequest);
        if (tokenResponse is null)
        {
            AuthTelemetry.RecordCookieRefreshFailed();
            context.RejectPrincipal();
            return;
        }

        ApplyRefreshedSession(context, tokenResponse, refreshRequest.RefreshToken);
    }

    private static bool TryCreateRefreshRequest(
        CookieValidatePrincipalContext context,
        out RefreshRequest refreshRequest)
    {
        refreshRequest = default;

        if (!TryGetExpiration(context, out var expiresAt))
            return false;

        if (!IsRefreshRequired(context, expiresAt))
            return false;

        if (!TryGetRefreshToken(context, out var refreshToken))
        {
            AuthTelemetry.RecordMissingRefreshToken();
            context.RejectPrincipal();
            return false;
        }

        refreshRequest = new RefreshRequest(GetKeycloakOptions(context), refreshToken);
        return true;
    }

    private static bool TryGetExpiration(
        CookieValidatePrincipalContext context,
        out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        var expiresAtStr = context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.ExpiresAt);
        return expiresAtStr is not null
            && DateTimeOffset.TryParse(expiresAtStr, out expiresAt);
    }

    private static bool IsRefreshRequired(
        CookieValidatePrincipalContext context,
        DateTimeOffset expiresAt)
    {
        var bffOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<BffOptions>>().Value;

        return expiresAt - DateTimeOffset.UtcNow
            <= TimeSpan.FromMinutes(bffOptions.TokenRefreshThresholdMinutes);
    }

    private static bool TryGetRefreshToken(
        CookieValidatePrincipalContext context,
        out string refreshToken)
    {
        refreshToken = context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken) ?? string.Empty;
        return !string.IsNullOrEmpty(refreshToken);
    }

    private static async Task<KeycloakTokenResponse?> TryRefreshSessionAsync(
        CookieValidatePrincipalContext context,
        RefreshRequest refreshRequest)
    {
        var tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            refreshRequest.KeycloakOptions.AuthServerUrl,
            refreshRequest.KeycloakOptions.Realm);
        using var client = CreateTokenClient(context);

        try
        {
            using var response = await SendRefreshRequestAsync(
                context,
                client,
                tokenEndpoint,
                refreshRequest);

            if (!response.IsSuccessStatusCode)
            {
                AuthTelemetry.RecordTokenEndpointRejected();
                return null;
            }

            return await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            AuthTelemetry.RecordTokenRefreshException(ex);
            GetLogger(context).LogWarning(ex, "Token refresh failed, rejecting principal.");
            return null;
        }
    }

    private static HttpClient CreateTokenClient(CookieValidatePrincipalContext context)
    {
        return context.HttpContext.RequestServices
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(AuthConstants.HttpClients.KeycloakToken);
    }

    private static Task<HttpResponseMessage> SendRefreshRequestAsync(
        CookieValidatePrincipalContext context,
        HttpClient client,
        string tokenEndpoint,
        RefreshRequest refreshRequest)
    {
        return client.PostAsync(
            tokenEndpoint,
            BuildRefreshRequestContent(refreshRequest.KeycloakOptions, refreshRequest.RefreshToken),
            context.HttpContext.RequestAborted);
    }

    private static KeycloakOptions GetKeycloakOptions(CookieValidatePrincipalContext context)
    {
        return context.HttpContext.RequestServices
            .GetRequiredService<IOptions<KeycloakOptions>>().Value;
    }


    private static FormUrlEncodedContent BuildRefreshRequestContent(
        KeycloakOptions keycloakOptions,
        string refreshToken)
    {
        var formParams = new Dictionary<string, string>
        {
            [AuthConstants.OAuth2FormParameters.GrantType] = AuthConstants.OAuth2GrantTypes.RefreshToken,
            [AuthConstants.OAuth2FormParameters.ClientId] = keycloakOptions.Resource,
            [AuthConstants.OAuth2FormParameters.RefreshToken] = refreshToken
        };

        if (!string.IsNullOrEmpty(keycloakOptions.Credentials.Secret))
            formParams[AuthConstants.OAuth2FormParameters.ClientSecret] = keycloakOptions.Credentials.Secret;

        return new FormUrlEncodedContent(formParams);
    }

    private static void ApplyRefreshedSession(
        CookieValidatePrincipalContext context,
        KeycloakTokenResponse tokenResponse,
        string refreshToken)
    {
        context.Properties.UpdateTokenValue(AuthConstants.CookieTokenNames.AccessToken, tokenResponse.AccessToken);
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.RefreshToken,
            tokenResponse.RefreshToken ?? refreshToken);
        context.Properties.UpdateTokenValue(
            AuthConstants.CookieTokenNames.ExpiresAt,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o"));
        context.ShouldRenew = true;
    }

    private static ILogger GetLogger(CookieValidatePrincipalContext context)
    {
        return context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(CookieSessionRefresher));
    }

    private readonly record struct RefreshRequest(
        KeycloakOptions KeycloakOptions,
        string RefreshToken);
}
