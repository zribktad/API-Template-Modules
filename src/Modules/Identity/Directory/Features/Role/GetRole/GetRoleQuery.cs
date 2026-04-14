using ErrorOr;
using Identity.Directory.Features.Role.Shared;

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
        var role = await repository.FirstOrDefaultAsync(
            new RoleByIdForTenantSpecification(query.Id, tenantProvider.TenantId),
            ct
        );
        if (role == null)
            return DomainErrors.Roles.NotFound(query.Id);

        return role;
    }
}
