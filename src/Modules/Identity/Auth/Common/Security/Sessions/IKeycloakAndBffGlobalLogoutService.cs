namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Orchestrates Keycloak admin logout-all (IdP) and BFF persisted session revocation for a subject.
/// </summary>
public interface IKeycloakAndBffGlobalLogoutService
{
    /// <summary>
    ///     Terminates all Keycloak sessions for the user, then revokes every BFF session for the same Keycloak subject.
    /// </summary>
    Task SignOutEverywhereAsync(
        string keycloakUserId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    );
}
