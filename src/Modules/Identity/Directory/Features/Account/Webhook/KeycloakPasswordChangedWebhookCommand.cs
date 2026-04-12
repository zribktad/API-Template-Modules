using ErrorOr;
using Identity.Auth.Security.Sessions;
using Identity.Errors;
using Wolverine;

namespace Identity.Directory.Features.Account;

public sealed record KeycloakPasswordChangedWebhookCommand(string KeycloakUserId);

public sealed class KeycloakPasswordChangedWebhookCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        KeycloakPasswordChangedWebhookCommand command,
        IKeycloakAndBffGlobalLogoutService globalLogout,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(command.KeycloakUserId))
            return Error.Validation(
                ErrorCatalog.KeycloakWebhook.MissingUserId,
                "KeycloakUserId is required."
            );

        await globalLogout.SignOutEverywhereAsync(
            command.KeycloakUserId,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );

        return Result.Success;
    }
}
