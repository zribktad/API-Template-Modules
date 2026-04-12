using Ardalis.Specification;
using UserEntity = Identity.Directory.Entities.AppUser;

namespace Identity.Directory.Features.Tenant;

/// <summary>
///     Loads all non-deleted users for a specific tenant, bypassing global query filters
///     so the spec works correctly in handlers that run without a tenant context.
/// </summary>
public sealed class UsersForTenantSoftDeleteSpecification : Specification<UserEntity>
{
    public UsersForTenantSoftDeleteSpecification(Guid tenantId)
    {
        Query.Where(user => user.TenantId == tenantId && !user.IsDeleted).IgnoreQueryFilters();
    }
}
