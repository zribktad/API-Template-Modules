using ErrorOr;
using Identity.Application.Features.User.DTOs;
using Identity.Application.Logging;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User;

public sealed record KeycloakPasswordResetCommand(RequestPasswordResetRequest Request);

public sealed class KeycloakPasswordResetCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        KeycloakPasswordResetCommand command,
        IUserRepository repository,
        IKeycloakAdminService keycloakAdmin,
        ILogger<KeycloakPasswordResetCommandHandler> logger,
        CancellationToken ct
    )
    {
        AppUser? user = await repository.FindByEmailAsync(command.Request.Email, ct);

        if (user is null || user.KeycloakUserId is null)
            return Result.Success;

        try
        {
            await keycloakAdmin.SendPasswordResetEmailAsync(user.KeycloakUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.PasswordResetEmailFailed(ex, user.Id);
        }

        return Result.Success;
    }
}
