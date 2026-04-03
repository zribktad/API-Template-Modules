using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.SoftDelete;

/// <summary>
/// Orchestrates recursive soft-delete processing for tracked EF Core entities.
/// </summary>
public interface ISoftDeleteProcessor
{
    Task ProcessAsync(
        DbContext dbContext,
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        IReadOnlyCollection<ISoftDeleteCascadeRule> softDeleteCascadeRules,
        CancellationToken cancellationToken
    );
}
