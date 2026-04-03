using Ardalis.Specification;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.TenantCascadeDelete;

/// <summary>
/// Loads all non-deleted products (with their data links) for a specific tenant, bypassing
/// global query filters so the spec works correctly in cross-module handlers that run without
/// a tenant context. Including <c>ProductDataLinks</c> ensures the cascade rule can soft-delete
/// them in the same transaction.
/// </summary>
public sealed class ProductsForTenantSoftDeleteSpecification : Specification<ProductEntity>
{
    public ProductsForTenantSoftDeleteSpecification(Guid tenantId)
    {
        Query
            .Where(product => product.TenantId == tenantId && !product.IsDeleted)
            .Include(product => product.ProductDataLinks)
            .IgnoreQueryFilters();
    }
}
