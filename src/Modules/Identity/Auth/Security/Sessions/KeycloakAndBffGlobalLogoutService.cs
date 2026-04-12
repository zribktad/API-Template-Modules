using Identity.Auth.Security;
using Identity.Logging;
using Microsoft.Extensions.Logging;

namespace Identity.Auth.Security.Sessions;

public sealed class KeycloakAndBffGlobalLogoutService : IKeycloakAndBffGlobalLogoutService
{
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly IBffSessionRevocationService _bffSessionRevocation;
    private readonly ILogger<KeycloakAndBffGlobalLogoutService> _logger;

    public KeycloakAndBffGlobalLogoutService(
        IKeycloakAdminService keycloakAdmin,
        IBffSessionRevocationService bffSessionRevocation,
        ILogger<KeycloakAndBffGlobalLogoutService> logger
    )
    {
        _keycloakAdmin = keycloakAdmin;
        _bffSessionRevocation = bffSessionRevocation;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SignOutEverywhereAsync(
        string keycloakUserId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    )
    {
        try
        {
            await _keycloakAdmin.LogoutAllUserSessionsAsync(keycloakUserId, ct);
        }
        catch (Exception ex)
        {
            _logger.KeycloakLogoutAllSessionsFailed(ex, keycloakUserId);
        }

        await _bffSessionRevocation.RevokeAllSessionsForSubjectAsync(keycloakUserId, reason, ct);
    }
}
