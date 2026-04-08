using ErrorOr;
using Wolverine;

namespace Identity.Features.User;

public sealed record SetUserActiveCommand(Guid Id, bool IsActive) : IHasId;

public sealed class SetUserActiveCommandHandler
{
    public static async Task<ErrorOr<AppUser>> ValidateAsync(
        SetUserActiveCommand command,
        IUserRepository repository,
        CancellationToken ct
    ) =>
        await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Users.NotFound(command.Id),
            ct
        );

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        SetUserActiveCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IKeycloakAdminService keycloakAdmin,
        ErrorOr<AppUser> userResult,
        CancellationToken ct
    )
    {
        if (userResult.IsError)
            return (userResult.Errors, OutgoingMessagesHelper.Empty);
        AppUser user = userResult.Value;

        if (user.KeycloakUserId is not null)
            await keycloakAdmin.SetUserEnabledAsync(user.KeycloakUserId, command.IsActive, ct);

        user.IsActive = command.IsActive;
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
