using Microsoft.EntityFrameworkCore;

namespace Identity.SoftDelete;

/// <summary>
///     Soft-delete cascade rule for the Tenant aggregate.
///     Cascades to Users and TenantInvitations within this module.
///     Cross-module cascade (Products, Categories) is handled via <see cref="TenantSoftDeletedNotification" />.
/// </summary>
public sealed class TenantSoftDeleteCascadeRule : ISoftDeleteCascadeRule
{
    public bool CanHandle(IAuditableTenantEntity entity)
    {
        return entity is Tenant;
    }

    public async Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        DbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (entity is not Tenant tenant || dbContext is not IdentityDbContext db)
            return [];

        List<IAuditableTenantEntity> dependents = [];

        dependents.AddRange(
            await db
                .Users.IgnoreQueryFilters([
                    GlobalQueryFilterNames.SoftDelete,
                    GlobalQueryFilterNames.Tenant,
                ])
                .Where(u => u.TenantId == tenant.Id && !u.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await db
                .TenantInvitations.IgnoreQueryFilters([
                    GlobalQueryFilterNames.SoftDelete,
                    GlobalQueryFilterNames.Tenant,
                ])
                .Where(i => i.TenantId == tenant.Id && !i.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        return dependents;
    }
}
