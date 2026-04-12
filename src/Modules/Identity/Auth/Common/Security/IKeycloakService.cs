using Identity.Auth.Security.Keycloak;

namespace Identity.Auth.Security;

/// <summary>
///     Infrastructure-facing Keycloak port for non-admin authentication flows such as BFF session
///     token refresh.
/// </summary>
public interface IKeycloakService
{
    /// <summary>
    ///     Uses the refresh-token grant to obtain a fresh Keycloak token response for the current
    ///     BFF session and returns an explicit outcome so the caller can distinguish a rejected
    ///     refresh from transport/provider failures.
    /// </summary>
    public Task<KeycloakRefreshResult> RefreshSessionAsync(
        string refreshToken,
        CancellationToken ct = default
    );
}
