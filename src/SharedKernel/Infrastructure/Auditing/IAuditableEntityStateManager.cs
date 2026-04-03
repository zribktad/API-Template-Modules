using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.Auditing;

/// <summary>
/// Abstracts audit-field stamping for auditable entities tracked by EF Core.
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
