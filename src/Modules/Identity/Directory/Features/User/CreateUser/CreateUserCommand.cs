using ErrorOr;
using Identity.Directory.Domain.Services;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record CreateUserCommand(CreateUserRequest Request);

public sealed class CreateUserCommandHandler
{
    public static async Task<ErrorOr<Success>> ValidateAsync(
        CreateUserCommand command,
        IUserUniquenessChecker uniqueness,
        CancellationToken ct
    )
    {
        return await uniqueness.EnsureUniqueAsync(
            command.Request.Username,
            command.Request.Email,
            ct
        );
    }

    public static async Task<(ErrorOr<UserResponse>, OutgoingMessages)> HandleAsync(
        CreateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<Success> validation,
        CancellationToken ct
    )
    {
        if (validation.IsError)
            return (validation.Errors, OutgoingMessagesHelper.Empty);

        AppUser user = AppUser.Create(command.Request.Username, command.Request.Email, keycloakUserId: null);

        await repository.AddAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new ProvisionKeycloakUserEvent(user.Id, user.Username.Value, user.Email.Value));
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (user.ToResponse(), messages);
    }
}
