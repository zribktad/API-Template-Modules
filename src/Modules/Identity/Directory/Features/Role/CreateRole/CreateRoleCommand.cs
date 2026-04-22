using ErrorOr;
using Identity.Directory.Features.Role.Shared;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleCommand(CreateRoleRequest Request) : IHasId
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class CreateRoleCommandHandler
{
    public static async Task<(ErrorOr<RoleResponse>, OutgoingMessages)> HandleAsync(
        CreateRoleCommand command,
        IRoleRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ITenantProvider tenantProvider,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken ct
    )
    {
        bool isPlatformAdmin = httpContextAccessor.HttpContext?.User.IsPlatformAdmin() == true;
        Guid? resolvedTenantId = isPlatformAdmin ? command.Request.TenantId : tenantProvider.TenantId;

        ErrorOr<CustomRole> roleResult = CustomRole.Create(
            command.Id,
            command.Request.Name,
            resolvedTenantId,
            command.Request.Permissions,
            isPlatformAdmin
        );
        if (roleResult.IsError)
            return (roleResult.FirstError, OutgoingMessagesHelper.Empty);

        await repository.AddAsync(roleResult.Value, ct);
        await unitOfWork.CommitAsync(ct);

        return (roleResult.Value.ToResponse(), OutgoingMessagesHelper.Empty);
    }
}
