using Ardalis.Specification;

namespace Identity.Directory.Features.Role.Shared;

public sealed class RoleByIdForTenantSpecification
    : SingleResultSpecification<CustomRole, RoleResponse>
{
    public RoleByIdForTenantSpecification(Guid id, Guid tenantId)
    {
        Query
            .Where(r => r.Id == id && (r.TenantId == null || r.TenantId == tenantId))
            .AsNoTracking()
            .Select(RoleMappings.Projection);
    }
}
