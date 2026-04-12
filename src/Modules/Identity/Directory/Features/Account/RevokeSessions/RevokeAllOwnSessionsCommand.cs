using ErrorOr;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Wolverine;

namespace Identity.Directory.Features.Account;

public sealed record RevokeAllOwnSessionsCommand(string KeycloakUserId);

public sealed class RevokeAllOwnSessionsCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        RevokeAllOwnSessionsCommand command,
        IKeycloakAdminService keycloakAdmin,
        IBffSessionRevocationService sessionRevocation,
        CancellationToken ct
    )
    {
        await keycloakAdmin.LogoutAllUserSessionsAsync(command.KeycloakUserId, ct);
        await sessionRevocation.RevokeAllSessionsForSubjectAsync(
            command.KeycloakUserId,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );

        return Result.Success;
    }
}
