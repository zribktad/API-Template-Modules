using ErrorOr;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Interfaces;
using SharedKernel.Application.Context;

namespace Identity.Directory.Features.Role.GetRole;

public sealed record GetRoleQuery(Guid Id);

public sealed class GetRoleQueryHandler
{
    public static async Task<ErrorOr<RoleResponse>> HandleAsync(
        GetRoleQuery query,
        IRoleRepository repository,
        ITenantProvider tenantProvider,
        CancellationToken ct
    )
    {
        var role = await repository.FirstOrDefaultAsync(new RoleByIdSpecification(query.Id), ct);
        if (role == null)
            return Error.NotFound("Role.NotFound", "Role not found.");

        bool visible = role.TenantId == null || role.TenantId == tenantProvider.TenantId;
        if (!visible)
            return Error.NotFound("Role.NotFound", "Role not found.");

        return role.ToResponse();
    }
}
