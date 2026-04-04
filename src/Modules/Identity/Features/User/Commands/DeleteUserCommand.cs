using ErrorOr;
using Identity.Logging;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Features.User;

public sealed record DeleteUserCommand(Guid Id) : IHasId;

public sealed class DeleteUserCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
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
            return (userResult.Errors, OutgoingMessagesHelper.Empty);
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
            logger.DeleteUserDbDeleteFailed(ex, user.KeycloakUserId);
            throw;
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
