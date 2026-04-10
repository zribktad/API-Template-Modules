using Identity.Security.Keycloak;

namespace Identity.Security;

/// <summary>
///     Infrastructure-facing Keycloak port for non-admin authentication flows such as BFF session
///     token refresh.
/// </summary>
public interface IKeycloakService
{
    /// <summary>
    ///     Uses the refresh-token grant to obtain a fresh Keycloak token response for the current
    ///     BFF session. Returns <c>null</c> when Keycloak rejects the refresh or returns an invalid
    ///     payload.
    /// </summary>
    public Task<KeycloakTokenResponse?> RefreshSessionAsync(
        string refreshToken,
        CancellationToken ct = default
    );
}
