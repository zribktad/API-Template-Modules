using Ardalis.Specification;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.Shared;

public sealed class RolesByTenantIdSpecification : Specification<CustomRole>
{
    public RolesByTenantIdSpecification(Guid tenantId)
    {
        Query.Where(r => r.TenantId == tenantId || r.TenantId == null).Include(r => r.Permissions);
    }
}
