using BuildingBlocks.Domain.Entities.Contracts;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BuildingBlocks.Infrastructure.EFCore.Auditing;

/// <summary>
///     Abstracts audit-field stamping for auditable entities tracked by EF Core.
/// </summary>
public interface IAuditableEntityStateManager
{
    void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId
    );

    void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor);

    void MarkSoftDeleted(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor
    );
}
