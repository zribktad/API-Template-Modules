namespace APITemplate.Application.Common.Security;

/// <summary>
/// Application-layer port for managing Keycloak users via the Admin REST API.
/// Implementations live in the Infrastructure layer and communicate with Keycloak on behalf of the application.
/// </summary>
public interface IKeycloakAdminService
{
    /// <summary>Creates a new Keycloak user and returns the assigned Keycloak user ID.</summary>
    Task<string> CreateUserAsync(string username, string email, CancellationToken ct = default);

    /// <summary>Triggers a password-reset email for the specified Keycloak user.</summary>
    Task SendPasswordResetEmailAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>Enables or disables the specified Keycloak user account.</summary>
    Task SetUserEnabledAsync(string keycloakUserId, bool enabled, CancellationToken ct = default);

    /// <summary>Permanently deletes the specified Keycloak user.</summary>
    Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default);
}
