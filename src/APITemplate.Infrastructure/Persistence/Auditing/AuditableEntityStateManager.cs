using APITemplate.Domain.Entities;
using SharedKernel.Domain.Entities.Contracts;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.Auditing;

/// <summary>
/// Infrastructure implementation of <see cref="IAuditableEntityStateManager"/> that stamps
/// audit fields on EF Core entity entries in response to Add, Modify, and soft-delete state transitions.
/// </summary>
public sealed class AuditableEntityStateManager
    : SharedKernel.Infrastructure.Auditing.AuditableEntityStateManager,
        IAuditableEntityStateManager
{
    public override void StampAdded(
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

        base.StampAdded(entry, entity, now, actor, hasTenant, currentTenantId);
    }
}
