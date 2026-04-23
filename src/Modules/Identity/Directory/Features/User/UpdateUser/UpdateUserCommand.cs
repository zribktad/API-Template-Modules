using ErrorOr;
using Identity.Directory.Domain.Services;
using Wolverine;

namespace Identity.Directory.Features.User;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IHasId;

public sealed class UpdateUserCommandHandler
{
    public static async Task<ErrorOr<AppUser>> ValidateAsync(
        UpdateUserCommand command,
        IUserRepository repository,
        IUserUniquenessChecker uniqueness,
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

        if (!string.Equals(user.Email.Normalized, NormalizedString.Normalize(command.Request.Email), StringComparison.Ordinal))
        {
            ErrorOr<Success> emailResult = await uniqueness.EnsureEmailUniqueAsync(command.Request.Email, ct);
            if (emailResult.IsError)
                return emailResult.Errors;
        }

        string normalizedNew = NormalizedString.Normalize(command.Request.Username);
        if (!string.Equals(user.Username.Normalized, normalizedNew, StringComparison.Ordinal))
        {
            ErrorOr<Success> usernameResult = await uniqueness.EnsureUsernameUniqueAsync(
                command.Request.Username,
                ct
            );
            if (usernameResult.IsError)
                return usernameResult.Errors;
        }

        return user;
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        UpdateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<AppUser> validationResult,
        CancellationToken ct
    )
    {
        if (validationResult.IsError)
            return (validationResult.Errors, OutgoingMessagesHelper.Empty);
        AppUser user = validationResult.Value;

        user.Username = new NormalizedString(command.Request.Username);
        user.Email = new NormalizedString(command.Request.Email);

        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
