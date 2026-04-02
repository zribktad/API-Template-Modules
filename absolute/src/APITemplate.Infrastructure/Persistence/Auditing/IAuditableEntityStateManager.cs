using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.Auditing;

/// <summary>
/// Abstracts audit-field stamping for <see cref="IAuditableTenantEntity"/> instances tracked by EF Core,
/// covering the add, modify, and soft-delete state transitions.
/// </summary>
public interface IAuditableEntityStateManager
{
    /// <summary>Stamps creation audit fields and assigns tenant context when an entity is first added.</summary>
    void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId
    );

    /// <summary>Updates the last-modified audit fields when an entity changes.</summary>
    void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor);

    /// <summary>Converts a pending hard-delete into a soft-delete and stamps the deletion audit fields.</summary>
    void MarkSoftDeleted(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor
    );
}
