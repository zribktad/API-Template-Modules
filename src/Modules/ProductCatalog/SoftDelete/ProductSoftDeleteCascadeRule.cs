using Microsoft.EntityFrameworkCore;
using ProductCatalog.Persistence;

namespace ProductCatalog.SoftDelete;

public sealed class ProductSoftDeleteCascadeRule : ISoftDeleteCascadeRule
{
    public bool CanHandle(IAuditableTenantEntity entity) => entity is Product;

    public async Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        DbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (entity is not Product product || dbContext is not ProductCatalogDbContext productCatalogDbContext)
            return [];

        return await productCatalogDbContext
            .ProductDataLinks.IgnoreQueryFilters(["SoftDelete", "Tenant"])
            .Where(link => link.ProductId == product.Id && link.TenantId == product.TenantId && !link.IsDeleted)
            .Cast<IAuditableTenantEntity>()
            .ToListAsync(cancellationToken);
    }
}

