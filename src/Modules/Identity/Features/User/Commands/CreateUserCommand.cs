using ErrorOr;
using Identity.Features.User.Mappings;
using Identity.Logging;
using Identity.ValueObjects;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Features.User;

public sealed record CreateUserCommand(CreateUserRequest Request);

public sealed class CreateUserCommandHandler
{
    public static async Task<(ErrorOr<UserResponse>, OutgoingMessages)> HandleAsync(
        CreateUserCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ILogger<CreateUserCommandHandler> logger,
        IKeycloakAdminService keycloakAdmin,
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

        string keycloakUserId = await keycloakAdmin.CreateUserAsync(
            command.Request.Username,
            command.Request.Email,
            ct
        );

        try
        {
            AppUser user = new()
            {
                Id = Guid.NewGuid(),
                Username = command.Request.Username,
                Email = email,
                KeycloakUserId = keycloakUserId,
            };

            await repository.AddAsync(user, ct);
            await unitOfWork.CommitAsync(ct);
            OutgoingMessages messages = new();
            messages.Add(new UserRegisteredNotification(user.Id, user.Email.Value, user.Username));
            messages.Add(new CacheInvalidationNotification(CacheTags.Users));
            return (user.ToResponse(), messages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.CreateUserDbSaveFailed(ex, keycloakUserId);
            try
            {
                await keycloakAdmin.DeleteUserAsync(keycloakUserId, CancellationToken.None);
            }
            catch (Exception compensationEx)
            {
                logger.CreateUserCompensatingDeleteFailed(compensationEx, keycloakUserId);
            }

            throw;
        }
    }
}
