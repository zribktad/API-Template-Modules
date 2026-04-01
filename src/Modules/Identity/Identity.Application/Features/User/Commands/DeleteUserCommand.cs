using Identity.Application.Common.Security;
using SharedKernel.Domain.Entities.Contracts;
using Identity.Domain.Interfaces;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Application.Events;
using SharedKernel.Application.Extensions;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Application.Features.User;

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
        ErrorOr<AppUser> userResult = await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Users.NotFound(command.Id),
            ct
        );
        if (userResult.IsError)
            return userResult.Errors;
        AppUser user = userResult.Value;

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
