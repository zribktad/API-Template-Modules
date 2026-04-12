using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Interfaces;
using Microsoft.AspNetCore.Http;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
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
        var user = httpContextAccessor.HttpContext?.User;
        bool isPlatformAdmin =
            user?.HasClaim(AuthConstants.Claims.Permission, Permission.Platform.Manage) == true;

        if (!isPlatformAdmin && command.Request.Permissions.Contains(Permission.Platform.Manage))
        {
            return (
                Error.Forbidden(
                    "Role.Permissions",
                    "TenantAdmin cannot grant Platform.Manage permission."
                ),
                OutgoingMessagesHelper.Empty
            );
        }

        var role = new CustomRole
        {
            Id = command.Id,
            Name = command.Request.Name,
            // PlatformAdmin can override TenantId, TenantAdmin always creates in their own tenant
            TenantId = isPlatformAdmin
                ? (command.Request.TenantId ?? tenantProvider.TenantId)
                : tenantProvider.TenantId,
            IsImmutable = false,
        };

        foreach (var perm in command.Request.Permissions)
        {
            role.Permissions.Add(
                new RolePermission
                {
                    RoleId = role.Id,
                    Permission = perm,
                    Role = role,
                }
            );
        }

        await repository.AddAsync(role, ct);
        await unitOfWork.CommitAsync(ct);

        return (role.ToResponse(), OutgoingMessagesHelper.Empty);
    }
}
