using APITemplate.Domain.Entities;

namespace APITemplate.Application.Common.Security;

/// <summary>
/// Application-layer port that ensures a local <see cref="AppUser"/> record exists for an authenticated
/// Keycloak identity, creating one on first login if necessary.
/// </summary>
public interface IUserProvisioningService
{
    /// <summary>
    /// Looks up the local user record for the given Keycloak identity and provisions it if it does not
    /// yet exist. Returns <see langword="null"/> when provisioning cannot be completed.
    /// </summary>
    Task<AppUser?> ProvisionIfNeededAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default
    );
}
