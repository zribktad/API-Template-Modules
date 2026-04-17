using ErrorOr;
using Identity.Directory.Domain.Services;
using Identity.ValueObjects;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record CreateUserCommand(CreateUserRequest Request);

public sealed class CreateUserCommandHandler
{
    public static async Task<ErrorOr<Email>> ValidateAsync(
        CreateUserCommand command,
        IUserUniquenessChecker uniqueness,
        CancellationToken ct
    )
    {
        ErrorOr<Email> emailResult = Email.Create(command.Request.Email);
        if (emailResult.IsError)
            return emailResult.Errors;

        ErrorOr<Success> uniqueResult = await uniqueness.EnsureUniqueAsync(
            command.Request.Username,
            emailResult.Value,
            ct
        );
        if (uniqueResult.IsError)
            return uniqueResult.Errors;

        return emailResult.Value;
    }

    public static async Task<(ErrorOr<UserResponse>, OutgoingMessages)> HandleAsync(
        CreateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<Email> validation,
        CancellationToken ct
    )
    {
        if (validation.IsError)
            return (validation.Errors, OutgoingMessagesHelper.Empty);
        Email email = validation.Value;

        AppUser user = AppUser.Create(command.Request.Username, email, keycloakUserId: null);

        await repository.AddAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new ProvisionKeycloakUserEvent(user.Id, user.Username, user.Email.Value));
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (user.ToResponse(), messages);
    }
}
