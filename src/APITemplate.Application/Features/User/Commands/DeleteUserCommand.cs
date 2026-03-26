using APITemplate.Application.Common.Errors;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Extensions;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace APITemplate.Application.Features.User;

public sealed record DeleteUserCommand(Guid Id) : IHasId;

public sealed class DeleteUserCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        DeleteUserCommand command,
        IUserRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        IKeycloakAdminService keycloakAdmin,
        ILogger<DeleteUserCommandHandler> logger,
        CancellationToken ct
    )
    {
        var userResult = await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Users.NotFound(command.Id),
            ct
        );
        if (userResult.IsError)
            return userResult.Errors;
        var user = userResult.Value;

        if (user.KeycloakUserId is not null)
            await keycloakAdmin.DeleteUserAsync(user.KeycloakUserId, ct);

        try
        {
            await repository.DeleteAsync(user, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(
                ex,
                "DB delete failed after Keycloak user {KeycloakUserId} was already deleted. Manual cleanup required.",
                user.KeycloakUserId
            );
            throw;
        }

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Users));
        return Result.Success;
    }
}
