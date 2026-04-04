using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.Auditing;

/// <summary>
///     Abstracts audit-field stamping for auditable entities tracked by EF Core.
/// </summary>
public interface IAuditableEntityStateManager
{
    public void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId
    );

    public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor);

    public void MarkSoftDeleted(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor
    );
}
