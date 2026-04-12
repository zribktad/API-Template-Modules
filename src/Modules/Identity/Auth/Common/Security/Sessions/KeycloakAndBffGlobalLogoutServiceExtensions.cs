namespace Identity.Auth.Security.Sessions;

public static class KeycloakAndBffGlobalLogoutServiceExtensions
{
    /// <summary>
    ///     Signs the user out of Keycloak and revokes local BFF rows after a password change or
    ///     equivalent credential rotation.
    /// </summary>
    public static Task SignOutAfterCredentialChangeAsync(
        this IKeycloakAndBffGlobalLogoutService service,
        string keycloakUserId,
        CancellationToken ct
    ) =>
        service.SignOutEverywhereAsync(
            keycloakUserId,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );
}
