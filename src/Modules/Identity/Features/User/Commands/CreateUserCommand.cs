using ErrorOr;
using Identity.Features.User.Events;
using Identity.Features.User.Mappings;
using Identity.ValueObjects;
using Wolverine;

namespace Identity.Features.User;

public sealed record CreateUserCommand(CreateUserRequest Request);

public sealed class CreateUserCommandHandler
{
    public static async Task<(ErrorOr<UserResponse>, OutgoingMessages)> HandleAsync(
        CreateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        ErrorOr<Email> emailValueResult = Email.Create(command.Request.Email);
        if (emailValueResult.IsError)
            return (emailValueResult.Errors, OutgoingMessagesHelper.Empty);
        Email email = emailValueResult.Value;

        ErrorOr<Success> emailResult = await UserValidationHelper.ValidateEmailUniqueAsync(
            repository,
            command.Request.Email,
            ct
        );
        if (emailResult.IsError)
            return (emailResult.Errors, OutgoingMessagesHelper.Empty);

        ErrorOr<Success> usernameResult = await UserValidationHelper.ValidateUsernameUniqueAsync(
            repository,
            command.Request.Username,
            ct
        );
        if (usernameResult.IsError)
            return (usernameResult.Errors, OutgoingMessagesHelper.Empty);

        AppUser user = AppUser.Create(command.Request.Username, email, keycloakUserId: null);

        await repository.AddAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new ProvisionKeycloakUserEvent(user.Id, user.Username, user.Email.Value));
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (user.ToResponse(), messages);
    }
}
