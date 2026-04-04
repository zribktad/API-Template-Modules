using System.Net.Http.Json;
using Identity.Logging;
using Identity.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security.Keycloak;

/// <summary>
///     Singleton service that acquires and caches a Keycloak service-account (client credentials) token.
///     Tokens are kept in memory until they expire; a 30-second safety margin prevents
///     using a token that is about to expire mid-flight.
/// </summary>
public sealed class KeycloakAdminTokenProvider : IDisposable
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<KeycloakOptions> _keycloakOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<KeycloakAdminTokenProvider> _logger;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public KeycloakAdminTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminTokenProvider> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _keycloakOptions = keycloakOptions;
        _logger = logger;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    /// <summary>
    ///     Returns a cached service-account access token, refreshing it via the Keycloak token endpoint
    ///     when it is absent or within the 30-second expiry margin. Thread-safe via <see cref="SemaphoreSlim" />.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsTokenValid())
            return _cachedToken!;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock.
            if (IsTokenValid())
                return _cachedToken!;

            KeycloakTokenResponse response = await FetchTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(response.AccessToken))
            {
                throw new InvalidOperationException(
                    "Keycloak token endpoint returned a response with an empty access_token."
                );
            }

            _cachedToken = response.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<KeycloakTokenResponse> FetchTokenAsync(CancellationToken cancellationToken)
    {
        KeycloakOptions keycloak = _keycloakOptions.Value;
        string tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            keycloak.AuthServerUrl,
            keycloak.Realm
        );

        using HttpClient client = _httpClientFactory.CreateClient(
            AuthConstants.HttpClients.KeycloakToken
        );
        using FormUrlEncodedContent content = new(
            new Dictionary<string, string>
            {
                [AuthConstants.OAuth2FormParameters.GrantType] = AuthConstants
                    .OAuth2GrantTypes
                    .ClientCredentials,
                [AuthConstants.OAuth2FormParameters.ClientId] = keycloak.Resource,
                [AuthConstants.OAuth2FormParameters.ClientSecret] = keycloak.Credentials.Secret,
            }
        );

        using HttpResponseMessage response = await client.PostAsync(
            tokenEndpoint,
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.KeycloakAdminTokenAcquisitionFailed((int)response.StatusCode, body);
            response.EnsureSuccessStatusCode(); // throws
        }

        KeycloakTokenResponse token =
            await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak token endpoint returned empty body.");

        return token;
    }

    private bool IsTokenValid()
    {
        return _cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - ExpiryMargin;
    }
}
