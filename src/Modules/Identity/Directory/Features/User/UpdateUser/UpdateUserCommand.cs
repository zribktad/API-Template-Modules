using ErrorOr;
using Identity.ValueObjects;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IHasId;

public sealed class UpdateUserCommandHandler
{
    public static async Task<ErrorOr<(AppUser User, Email Email)>> ValidateAsync(
        UpdateUserCommand command,
        IUserRepository repository,
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

        ErrorOr<Email> emailValueResult = Email.Create(command.Request.Email);
        if (emailValueResult.IsError)
            return emailValueResult.Errors;
        Email newEmail = emailValueResult.Value;

        if (!string.Equals(user.Email.Value, newEmail.Value, StringComparison.OrdinalIgnoreCase))
        {
            ErrorOr<Success> emailResult = await UserValidationHelper.ValidateEmailUniqueAsync(
                repository,
                command.Request.Email,
                ct
            );
            if (emailResult.IsError)
                return emailResult.Errors;
        }

        string normalizedNew = AppUser.NormalizeUsername(command.Request.Username);
        if (!string.Equals(user.NormalizedUsername, normalizedNew, StringComparison.Ordinal))
        {
            ErrorOr<Success> usernameResult =
                await UserValidationHelper.ValidateUsernameUniqueAsync(
                    repository,
                    command.Request.Username,
                    ct
                );
            if (usernameResult.IsError)
                return usernameResult.Errors;
        }

        return (user, newEmail);
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        UpdateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<(AppUser User, Email Email)> validationResult,
        CancellationToken ct
    )
    {
        if (validationResult.IsError)
            return (validationResult.Errors, OutgoingMessagesHelper.Empty);
        (AppUser user, Email email) = validationResult.Value;

        user.Username = command.Request.Username;
        user.Email = email;

        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
