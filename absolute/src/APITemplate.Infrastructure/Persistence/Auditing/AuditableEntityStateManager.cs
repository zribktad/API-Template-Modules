using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.Auditing;

/// <summary>
/// Infrastructure implementation of <see cref="IAuditableEntityStateManager"/> that stamps
/// audit fields on EF Core entity entries in response to Add, Modify, and soft-delete state transitions.
/// </summary>
public sealed class AuditableEntityStateManager : IAuditableEntityStateManager
{
    /// <summary>
    /// Stamps creation audit fields, assigns the tenant ID when one is active, resets soft-delete
    /// flags, and ensures the entity entry state is <see cref="EntityState.Added"/>.
    /// </summary>
    public void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId
    )
    {
        if (entity is Tenant tenant && tenant.TenantId == Guid.Empty)
            tenant.TenantId = tenant.Id;

        if (entity.TenantId == Guid.Empty && hasTenant)
            entity.TenantId = currentTenantId;

        entity.Audit.CreatedAtUtc = now;
        entity.Audit.CreatedBy = actor;
        StampModified(entity, now, actor);
        ResetSoftDelete(entity);
        entry.State = EntityState.Added;
    }

    /// <summary>Updates the <c>UpdatedAtUtc</c> and <c>UpdatedBy</c> audit fields.</summary>
    public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor)
    {
        entity.Audit.UpdatedAtUtc = now;
        entity.Audit.UpdatedBy = actor;
    }

    /// <summary>
    /// Converts a hard-delete entry to a soft-delete by switching the entry state to Modified
    /// and setting <c>IsDeleted</c>, <c>DeletedAtUtc</c>, and <c>DeletedBy</c>.
    /// Also ensures the owned <see cref="AuditInfo"/> entry is marked Modified.
    /// </summary>
    public void MarkSoftDeleted(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor
    )
    {
        entry.State = EntityState.Modified;
        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.DeletedBy = actor;
        StampModified(entity, now, actor);
        EnsureAuditOwnedEntryState(entry, now, actor);
    }

    private static void ResetSoftDelete(IAuditableTenantEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.DeletedBy = null;
    }

    private static void EnsureAuditOwnedEntryState(EntityEntry ownerEntry, DateTime now, Guid actor)
    {
        var auditEntry = ownerEntry.Reference(nameof(IAuditableTenantEntity.Audit)).TargetEntry;
        if (auditEntry is null)
            return;

        if (
            auditEntry.State is EntityState.Deleted or EntityState.Detached or EntityState.Unchanged
        )
            auditEntry.State = EntityState.Modified;

        auditEntry.Property(nameof(AuditInfo.UpdatedAtUtc)).CurrentValue = now;
        auditEntry.Property(nameof(AuditInfo.UpdatedBy)).CurrentValue = actor;
    }
}
