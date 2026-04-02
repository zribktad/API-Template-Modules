using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Persistence.SoftDelete;

/// <summary>
/// Explicit soft-delete cascade rule for Product aggregate.
/// When a <see cref="Product"/> is soft-deleted, all active reviews belonging
/// to the same tenant are soft-deleted as well.
/// </summary>
public sealed class ProductSoftDeleteCascadeRule : ISoftDeleteCascadeRule
{
    /// <summary>
    /// Handles only <see cref="Product"/> entities.
    /// </summary>
    public bool CanHandle(IAuditableTenantEntity entity) => entity is Product;

    /// <summary>
    /// Returns active product reviews that belong to the same product and tenant.
    /// Query filters are intentionally ignored because dependent rows may already
    /// be filtered from normal query paths during delete operations.
    /// </summary>
    public async Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        AppDbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (entity is not Product product)
            return [];

        var dependents = new List<IAuditableTenantEntity>();

        dependents.AddRange(
            await dbContext
                .ProductReviews.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(r =>
                    r.ProductId == product.Id && r.TenantId == product.TenantId && !r.IsDeleted
                )
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await dbContext
                .ProductDataLinks.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(d =>
                    d.ProductId == product.Id && d.TenantId == product.TenantId && !d.IsDeleted
                )
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        return dependents;
    }
}
