using ErrorOr;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.User.InvalidatePermissions;
using Identity.Directory.Features.User.Shared;
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
            return DomainErrors.Users.NotFound(command.UserId);
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

        List<Guid> distinctRoleIds = command.Request.RoleIds.Distinct().ToList();
        List<CustomRole> requestedRoles = await roleRepository.ListAsync(
            new RolesByIdsSpecification(distinctRoleIds),
            ct
        );

        if (requestedRoles.Count != distinctRoleIds.Count)
            return (DomainErrors.Roles.InvalidRoles(), OutgoingMessagesHelper.Empty);

        if (requestedRoles.Any(r => r.TenantId != null && r.TenantId != user.TenantId))
        {
            return (DomainErrors.Roles.CannotAssignForeignTenant(), OutgoingMessagesHelper.Empty);
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
