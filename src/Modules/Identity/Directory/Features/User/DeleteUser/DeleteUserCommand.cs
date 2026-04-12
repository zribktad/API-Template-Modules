using ErrorOr;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record DeleteUserCommand(Guid Id) : IHasId;

public sealed class DeleteUserCommandHandler
{
    public static async Task<ErrorOr<AppUser>> ValidateAsync(
        DeleteUserCommand command,
        IUserRepository repository,
        CancellationToken ct
    ) => await repository.GetByIdOrError(command.Id, DomainErrors.Users.NotFound(command.Id), ct);

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<AppUser> userResult,
        CancellationToken ct
    )
    {
        if (userResult.IsError)
            return (userResult.Errors, OutgoingMessagesHelper.Empty);
        AppUser user = userResult.Value;

        await repository.DeleteAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        if (user.KeycloakUserId is not null)
            messages.Add(new DeleteKeycloakUserEvent(user.KeycloakUserId));
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
