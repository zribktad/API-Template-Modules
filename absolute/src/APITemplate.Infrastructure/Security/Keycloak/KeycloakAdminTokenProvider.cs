using System.Net.Http.Json;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// Singleton service that acquires and caches a Keycloak service-account (client credentials) token.
/// Tokens are kept in memory until they expire; a 30-second safety margin prevents
/// using a token that is about to expire mid-flight.
/// </summary>
public sealed class KeycloakAdminTokenProvider : IDisposable
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<KeycloakOptions> _keycloakOptions;
    private readonly ILogger<KeycloakAdminTokenProvider> _logger;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

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

    /// <summary>
    /// Returns a cached service-account access token, refreshing it via the Keycloak token endpoint
    /// when it is absent or within the 30-second expiry margin. Thread-safe via <see cref="SemaphoreSlim"/>.
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

            var response = await FetchTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(response.AccessToken))
                throw new InvalidOperationException(
                    "Keycloak token endpoint returned a response with an empty access_token."
                );

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
        var keycloak = _keycloakOptions.Value;
        var tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            keycloak.AuthServerUrl,
            keycloak.Realm
        );

        using var client = _httpClientFactory.CreateClient(AuthConstants.HttpClients.KeycloakToken);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [AuthConstants.OAuth2FormParameters.GrantType] = AuthConstants
                    .OAuth2GrantTypes
                    .ClientCredentials,
                [AuthConstants.OAuth2FormParameters.ClientId] = keycloak.Resource,
                [AuthConstants.OAuth2FormParameters.ClientSecret] = keycloak.Credentials.Secret,
            }
        );

        using var response = await client.PostAsync(tokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to acquire Keycloak admin token. Status: {Status}. Body: {Body}",
                (int)response.StatusCode,
                body
            );
            response.EnsureSuccessStatusCode(); // throws
        }

        var token =
            await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak token endpoint returned empty body.");

        return token;
    }

    private bool IsTokenValid() =>
        _cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - ExpiryMargin;

    public void Dispose() => _lock.Dispose();
}
