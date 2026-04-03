using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharedKernel.Domain.Entities;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.Auditing;

/// <summary>
/// Default audit state manager that handles add/modify/soft-delete transitions for tenant-auditable entities.
/// </summary>
public class AuditableEntityStateManager : IAuditableEntityStateManager
{
    public virtual void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId
    )
    {
        if (entity.TenantId == Guid.Empty && hasTenant)
            entity.TenantId = currentTenantId;

        entity.Audit.CreatedAtUtc = now;
        entity.Audit.CreatedBy = actor;
        StampModified(entity, now, actor);
        ResetSoftDelete(entity);
        entry.State = EntityState.Added;
    }

    public virtual void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor)
    {
        entity.Audit.UpdatedAtUtc = now;
        entity.Audit.UpdatedBy = actor;
    }

    public virtual void MarkSoftDeleted(
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
        EntityEntry? auditEntry = ownerEntry
            .Reference(nameof(IAuditableTenantEntity.Audit))
            .TargetEntry;
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
