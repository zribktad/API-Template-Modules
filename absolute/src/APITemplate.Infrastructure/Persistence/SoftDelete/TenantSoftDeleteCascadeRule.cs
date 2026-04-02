using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Persistence.SoftDelete;

/// <summary>
/// Explicit soft-delete cascade rule for the Tenant aggregate.
/// When a <see cref="Tenant"/> is soft-deleted, all active users, products, and categories
/// belonging to that tenant are soft-deleted as well.
/// </summary>
public sealed class TenantSoftDeleteCascadeRule : ISoftDeleteCascadeRule
{
    /// <summary>Handles only <see cref="Tenant"/> entities.</summary>
    public bool CanHandle(IAuditableTenantEntity entity) => entity is Tenant;

    /// <summary>
    /// Returns active users, products, and categories that belong to the tenant,
    /// bypassing global query filters to ensure already-filtered rows are still found.
    /// </summary>
    public async Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        AppDbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (entity is not Tenant tenant)
            return [];

        var dependents = new List<IAuditableTenantEntity>();

        dependents.AddRange(
            await dbContext
                .Users.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(u => u.TenantId == tenant.Id && !u.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await dbContext
                .Products.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(p => p.TenantId == tenant.Id && !p.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await dbContext
                .Categories.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(c => c.TenantId == tenant.Id && !c.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        return dependents;
    }
}
