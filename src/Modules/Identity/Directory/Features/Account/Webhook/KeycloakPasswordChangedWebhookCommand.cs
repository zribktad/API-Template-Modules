using ErrorOr;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Errors;
using Wolverine;

namespace Identity.Directory.Features.Account;

public sealed record KeycloakPasswordChangedWebhookCommand(string KeycloakUserId);

public sealed class KeycloakPasswordChangedWebhookCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        KeycloakPasswordChangedWebhookCommand command,
        IKeycloakAdminService keycloakAdmin,
        IBffSessionRevocationService sessionRevocation,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(command.KeycloakUserId))
            return Error.Validation(
                ErrorCatalog.KeycloakWebhook.MissingUserId,
                "KeycloakUserId is required."
            );

        await keycloakAdmin.LogoutAllUserSessionsAsync(command.KeycloakUserId, ct);
        await sessionRevocation.RevokeAllSessionsForSubjectAsync(
            command.KeycloakUserId,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );

        return Result.Success;
    }
}
