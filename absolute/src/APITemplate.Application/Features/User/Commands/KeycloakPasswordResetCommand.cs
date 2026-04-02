using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace APITemplate.Application.Features.User;

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
        var user = await repository.FindByEmailAsync(command.Request.Email, ct);

        if (user is null || user.KeycloakUserId is null)
            return Result.Success;

        try
        {
            await keycloakAdmin.SendPasswordResetEmailAsync(user.KeycloakUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to send password reset email for user {UserId}.",
                user.Id
            );
        }

        return Result.Success;
    }
}
