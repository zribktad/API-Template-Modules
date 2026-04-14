using ErrorOr;
using Identity.Directory.Features.Role.InvalidatePermissions;
using Identity.Directory.Features.Role.Shared;
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
        if (role == null)
            return DomainErrors.Roles.NotFound(command.Id);
        if (role.IsImmutable)
            return DomainErrors.Roles.Immutable();
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
        if (roleResult.IsError)
            return (roleResult.Errors, OutgoingMessagesHelper.Empty);

        await repository.DeleteAsync(roleResult.Value, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new RolePermissionsInvalidatedEvent(command.Id));

        return (Result.Success, messages);
    }
}
