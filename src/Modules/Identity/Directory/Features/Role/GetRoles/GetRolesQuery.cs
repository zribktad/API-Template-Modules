using ErrorOr;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Interfaces;
using SharedKernel.Application.Context;

namespace Identity.Directory.Features.Role.GetRoles;

public sealed record GetRolesQuery();

public sealed class GetRolesQueryHandler
{
    public static async Task<ErrorOr<IReadOnlyList<RoleResponse>>> HandleAsync(
        GetRolesQuery query,
        IRoleRepository repository,
        ITenantProvider tenantProvider,
        CancellationToken ct
    )
    {
        var roles = await repository.ListAsync(
            new RolesByTenantIdSpecification(tenantProvider.TenantId),
            ct
        );
        return roles.Select(r => r.ToResponse()).ToList();
    }
}
