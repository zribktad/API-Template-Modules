using ErrorOr;
using Identity.Application.Features.User.DTOs;
using Identity.Domain;
using Identity.Domain.Interfaces;
using SharedKernel.Application.Events;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Identity.Application.Features.User;

public sealed record ChangeUserRoleCommand(Guid Id, ChangeUserRoleRequest Request) : IHasId;

public sealed class ChangeUserRoleCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        ChangeUserRoleCommand command,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
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

        string oldRole = user.Role.ToString();

        user.Role = command.Request.Role;
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(
            new UserRoleChangedNotification(
                user.Id,
                user.Email.Value,
                user.Username,
                oldRole,
                command.Request.Role.ToString()
            )
        );
        messages.Add(new CacheInvalidationNotification(CacheTags.Users));
        return (Result.Success, messages);
    }
}
