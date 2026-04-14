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

        if (!isPlatformAdmin && command.Request.Permissions.Contains(Permission.Platform.Manage))
            return (DomainErrors.Roles.CannotGrantPlatformManage(), OutgoingMessagesHelper.Empty);

        var role = new CustomRole
        {
            Id = command.Id,
            Name = command.Request.Name,
            // PlatformAdmin can set TenantId including null (global role); TenantAdmin always creates in their own tenant
            TenantId = isPlatformAdmin ? command.Request.TenantId : tenantProvider.TenantId,
            IsImmutable = false,
        };

        role.SetPermissions(command.Request.Permissions);

        await repository.AddAsync(role, ct);
        await unitOfWork.CommitAsync(ct);

        return (role.ToResponse(), OutgoingMessagesHelper.Empty);
    }
}
