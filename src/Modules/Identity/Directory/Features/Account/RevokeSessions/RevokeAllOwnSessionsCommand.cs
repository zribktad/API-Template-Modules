using ErrorOr;
using Identity.Auth.Security.Sessions;
using Wolverine;

namespace Identity.Directory.Features.Account;

public sealed record RevokeAllOwnSessionsCommand(string KeycloakUserId);

public sealed class RevokeAllOwnSessionsCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        RevokeAllOwnSessionsCommand command,
        IKeycloakAndBffGlobalLogoutService globalLogout,
        CancellationToken ct
    )
    {
        await globalLogout.SignOutEverywhereAsync(
            command.KeycloakUserId,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );

        return Result.Success;
    }
}
