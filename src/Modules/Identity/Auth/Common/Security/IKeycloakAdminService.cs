namespace Identity.Auth.Security;

/// <summary>
///     Application-layer port for Keycloak: Admin REST API (user lifecycle), logout-all, and verifying
///     credentials via the token endpoint (resource-owner grant on a dedicated confidential client).
/// </summary>
public interface IKeycloakAdminService
{
    /// <summary>Creates a new Keycloak user and returns the assigned Keycloak user ID.</summary>
    public Task<string> CreateUserAsync(
        string username,
        string email,
        CancellationToken ct = default
    );

    /// <summary>Triggers a password-reset email for the specified Keycloak user.</summary>
    public Task SendPasswordResetEmailAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>Enables or disables the specified Keycloak user account.</summary>
    public Task SetUserEnabledAsync(
        string keycloakUserId,
        bool enabled,
        CancellationToken ct = default
    );

    /// <summary>Permanently deletes the specified Keycloak user.</summary>
    public Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>
    ///     Sets the user's password via the Admin API. When <paramref name="temporary" /> is
    ///     <see langword="true" />, Keycloak requires a password change on next login.
    /// </summary>
    public Task SetUserPasswordAsync(
        string keycloakUserId,
        string newPassword,
        bool temporary,
        CancellationToken ct = default
    );

    /// <summary>Terminates all Keycloak sessions for the user (logout everywhere on the IdP).</summary>
    public Task LogoutAllUserSessionsAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>
    ///     Returns <see langword="true" /> when Keycloak issues tokens for the username/password
    ///     (resource-owner grant, server-side verification client).
    /// </summary>
    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken ct = default
    );
}
