using Identity.Auth.Security;

namespace Identity.Auth.Security.Sessions;

public sealed class KeycloakAndBffGlobalLogoutService : IKeycloakAndBffGlobalLogoutService
{
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly IBffSessionRevocationService _bffSessionRevocation;

    public KeycloakAndBffGlobalLogoutService(
        IKeycloakAdminService keycloakAdmin,
        IBffSessionRevocationService bffSessionRevocation
    )
    {
        _keycloakAdmin = keycloakAdmin;
        _bffSessionRevocation = bffSessionRevocation;
    }

    /// <inheritdoc />
    public async Task SignOutEverywhereAsync(
        string keycloakUserId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    )
    {
        await _keycloakAdmin.LogoutAllUserSessionsAsync(keycloakUserId, ct);
        await _bffSessionRevocation.RevokeAllSessionsForSubjectAsync(keycloakUserId, reason, ct);
    }
}
