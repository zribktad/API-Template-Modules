using System.Net.Http.Json;
using System.Text.Json;
using Identity.Logging;
using Identity.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security.Keycloak;

/// <summary>
///     Keycloak client facade for non-admin authentication endpoints used by the BFF session flow.
/// </summary>
public sealed class KeycloakService : IKeycloakService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakOptions _keycloakOptions;
    private readonly ILogger<KeycloakService> _logger;

    public KeycloakService(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakService> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _keycloakOptions = keycloakOptions.Value;
        _logger = logger;
    }

    public async Task<KeycloakRefreshResult> RefreshSessionAsync(
        string refreshToken,
        CancellationToken ct = default
    )
    {
        string tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            _keycloakOptions.AuthServerUrl,
            _keycloakOptions.Realm
        );

        HttpClient client = _httpClientFactory.CreateClient(
            AuthConstants.HttpClients.KeycloakToken
        );

        try
        {
            using HttpResponseMessage response = await client.PostAsync(
                tokenEndpoint,
                BuildRefreshContent(refreshToken),
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.KeycloakTokenEndpointReturnedNonSuccessDuringRefresh(
                    (int)response.StatusCode
                );
                string responseBody = await response.Content.ReadAsStringAsync(ct);
                if (IsRejectedRefresh(responseBody))
                    return new KeycloakRefreshResult(KeycloakRefreshStatus.Rejected);

                return new KeycloakRefreshResult(KeycloakRefreshStatus.ProviderError);
            }

            KeycloakTokenResponse? tokenResponse =
                await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct);
            if (!IsValidTokenResponse(tokenResponse))
            {
                _logger.KeycloakRefreshResponseInvalidRejectingPrincipal();
                return new KeycloakRefreshResult(KeycloakRefreshStatus.ProviderError);
            }

            return new KeycloakRefreshResult(KeycloakRefreshStatus.Success, tokenResponse);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.TokenRefreshFailedRejectingPrincipal(ex);
            return new KeycloakRefreshResult(KeycloakRefreshStatus.ProviderError);
        }
    }

    private FormUrlEncodedContent BuildRefreshContent(string refreshToken)
    {
        Dictionary<string, string> formParams = new()
        {
            [AuthConstants.OAuth2FormParameters.GrantType] = AuthConstants
                .OAuth2GrantTypes
                .RefreshToken,
            [AuthConstants.OAuth2FormParameters.ClientId] = _keycloakOptions.Resource,
            [AuthConstants.OAuth2FormParameters.RefreshToken] = refreshToken,
        };

        if (!string.IsNullOrEmpty(_keycloakOptions.Credentials.Secret))
        {
            formParams[AuthConstants.OAuth2FormParameters.ClientSecret] = _keycloakOptions
                .Credentials
                .Secret;
        }

        return new FormUrlEncodedContent(formParams);
    }

    private static bool IsValidTokenResponse(KeycloakTokenResponse? tokenResponse)
    {
        return tokenResponse is not null
            && !string.IsNullOrWhiteSpace(tokenResponse.AccessToken)
            && tokenResponse.ExpiresIn > 0;
    }

    private static bool IsRejectedRefresh(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("error", out JsonElement errorElement))
                return false;

            string? error = errorElement.GetString();
            return string.Equals(error, "invalid_grant", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
