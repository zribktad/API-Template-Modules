using Ardalis.Specification;
using CategoryEntity = ProductCatalog.Entities.Category;

namespace ProductCatalog.Features.Tenant.Specifications;

/// <summary>
/// Loads all non-deleted categories for a specific tenant, bypassing global query filters
/// so the spec works correctly in cross-module handlers that run without a tenant context.
/// </summary>
public sealed class CategoriesForTenantSoftDeleteSpecification : Specification<CategoryEntity>
{
    public CategoriesForTenantSoftDeleteSpecification(Guid tenantId)
    {
        Query
            .Where(category => category.TenantId == tenantId && !category.IsDeleted)
            .IgnoreQueryFilters();
    }
}

