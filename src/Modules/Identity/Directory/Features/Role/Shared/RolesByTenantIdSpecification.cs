using Ardalis.Specification;

namespace Identity.Directory.Features.Role.Shared;

public sealed class RolesByTenantIdSpecification : Specification<CustomRole, RoleResponse>
{
    public RolesByTenantIdSpecification(Guid tenantId)
    {
        Query
            .Where(r => r.TenantId == tenantId || r.TenantId == null)
            .AsNoTracking()
            .Select(RoleMappings.Projection);
    }
}
