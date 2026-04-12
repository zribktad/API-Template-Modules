using ErrorOr;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.User.Shared;
using Identity.Directory.Interfaces;
using SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Identity.Directory.Features.User.AssignRoles;

public sealed class AssignUserRolesCommandHandler
{
    public static async Task<ErrorOr<AppUser>> LoadAsync(
        AssignUserRolesCommand command,
        IUserRepository userRepository,
        CancellationToken ct
    )
    {
        var user = await userRepository.FirstOrDefaultAsync(
            new UserWithRolesByIdSpecification(command.UserId),
            ct
        );
        if (user == null)
            return Error.NotFound("User.NotFound", "User not found.");
        return user;
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        AssignUserRolesCommand command,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<AppUser> userResult,
        CancellationToken ct
    )
    {
        if (userResult.IsError)
            return (userResult.Errors, OutgoingMessagesHelper.Empty);
        var user = userResult.Value;

        var requestedRoles = await roleRepository.ListAsync(
            new RolesByIdsSpecification(command.Request.RoleIds),
            ct
        );

        if (requestedRoles.Count != command.Request.RoleIds.Count)
        {
            return (
                Error.Validation("Roles.Invalid", "One or more requested roles do not exist."),
                OutgoingMessagesHelper.Empty
            );
        }

        user.Roles.Clear();
        foreach (var role in requestedRoles)
        {
            user.Roles.Add(role);
        }

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new UserPermissionsInvalidatedEvent(user.Id, user.KeycloakUserId));

        return (Result.Success, messages);
    }
}

public sealed record UserPermissionsInvalidatedEvent(Guid AppUserId, string? KeycloakUserId);
