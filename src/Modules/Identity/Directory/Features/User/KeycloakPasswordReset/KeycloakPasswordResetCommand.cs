using ErrorOr;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record KeycloakPasswordResetCommand(RequestPasswordResetRequest Request);

public sealed class KeycloakPasswordResetCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        KeycloakPasswordResetCommand command,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        AppUser? user = await repository.FindByEmailAsync(command.Request.Email, ct);

        if (user is null || user.KeycloakUserId is null)
            return (Result.Success, OutgoingMessagesHelper.Empty);

        OutgoingMessages messages = new();
        messages.Add(new PasswordResetRequestedEvent(user.KeycloakUserId));
        return (Result.Success, messages);
    }
}
