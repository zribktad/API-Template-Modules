using ErrorOr;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Interfaces;
using SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Identity.Directory.Features.Role.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : IHasId;

public sealed class DeleteRoleCommandHandler
{
    public static async Task<ErrorOr<CustomRole>> LoadAsync(
        DeleteRoleCommand command,
        IRoleRepository repository,
        CancellationToken ct
    )
    {
        var role = await repository.FirstOrDefaultAsync(new RoleByIdSpecification(command.Id), ct);
        if (role == null) return Error.NotFound("Role.NotFound", "Role not found.");
        if (role.IsImmutable) return Error.Validation("Role.Immutable", "Cannot modify built-in roles.");
        return role;
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteRoleCommand command,
        IRoleRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ErrorOr<CustomRole> roleResult,
        CancellationToken ct
    )
    {
        if (roleResult.IsError) return (roleResult.Errors, OutgoingMessagesHelper.Empty);

        await repository.DeleteAsync(roleResult.Value, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new Identity.Directory.Features.User.InvalidatePermissions.RolePermissionsInvalidatedEvent(command.Id));

        return (Result.Success, messages);
    }
}
