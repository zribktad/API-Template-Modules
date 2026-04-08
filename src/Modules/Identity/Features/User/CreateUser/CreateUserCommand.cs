using ErrorOr;
using Identity.ValueObjects;
using Wolverine;

namespace Identity.Features.User;

public sealed record CreateUserCommand(CreateUserRequest Request);

public sealed class CreateUserCommandHandler
{
    // Wolverine lifecycle method — runs before HandleAsync; result is injected as a parameter
    public static async Task<ErrorOr<Success>> ValidateAsync(
        CreateUserCommand command,
        IUserRepository repository,
        CancellationToken ct
    )
    {
        ErrorOr<Success> emailResult = await UserValidationHelper.ValidateEmailUniqueAsync(
            repository,
            command.Request.Email,
            ct
        );
        if (emailResult.IsError)
            return emailResult.Errors;

        return await UserValidationHelper.ValidateUsernameUniqueAsync(
            repository,
            command.Request.Username,
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

        ErrorOr<Email> emailValueResult = Email.Create(command.Request.Email);
        if (emailValueResult.IsError)
            return (emailValueResult.Errors, OutgoingMessagesHelper.Empty);
        Email email = emailValueResult.Value;

        AppUser user = AppUser.Create(command.Request.Username, email, keycloakUserId: null);

        await repository.AddAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new ProvisionKeycloakUserEvent(user.Id, user.Username, user.Email.Value));
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (user.ToResponse(), messages);
    }
}
