using Identity.Security.Tenant;

namespace Identity.Security;

/// <summary>
///     Application-layer port that resolves whether a Keycloak-authenticated identity may access the
///     application: links or creates <see cref="AppUser" /> when an accepted invitation exists, or
///     returns a denial with a stable error code.
/// </summary>
public interface IUserProvisioningService
{
    /// <summary>
    ///     Ensures a local <see cref="AppUser" /> exists for the Keycloak identity when allowed by
    ///     invitation / admin flow; otherwise returns <see cref="UserAccessResolution.Denied" />.
    /// </summary>
    public Task<UserAccessResolution> ResolveAppUserAccessAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default
    );
}
