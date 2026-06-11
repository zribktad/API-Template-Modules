using ErrorOr;
using Identity.Directory.Features.Role.InvalidatePermissions;
using Identity.Directory.Features.Role.Shared;
using Wolverine;

namespace Identity.Directory.Features.Role.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : IHasId;

public sealed class DeleteRoleCommandHandler
{
    public static Task<ErrorOr<CustomRole>> LoadAsync(
        DeleteRoleCommand command,
        IRoleRepository repository,
        ITenantProvider tenantProvider,
        CancellationToken ct
    ) => RoleLoader.LoadMutableAsync(command.Id, tenantProvider.TenantId, repository, ct);

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
