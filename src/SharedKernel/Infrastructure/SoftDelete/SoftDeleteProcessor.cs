using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Infrastructure.Auditing;

namespace SharedKernel.Infrastructure.SoftDelete;

/// <summary>
/// Default implementation that recursively soft-deletes an entity and all dependents surfaced by cascade rules.
/// </summary>
public class SoftDeleteProcessor : ISoftDeleteProcessor
{
    private readonly IAuditableEntityStateManager _stateManager;

    public SoftDeleteProcessor(IAuditableEntityStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public Task ProcessAsync(
        DbContext dbContext,
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        IReadOnlyCollection<ISoftDeleteCascadeRule> softDeleteCascadeRules,
        CancellationToken cancellationToken
    )
    {
        var visited = new HashSet<IAuditableTenantEntity>(ReferenceEqualityComparer.Instance);
        return SoftDeleteWithRulesAsync(
            dbContext,
            entry,
            entity,
            now,
            actor,
            softDeleteCascadeRules,
            visited,
            cancellationToken
        );
    }

    private async Task SoftDeleteWithRulesAsync(
        DbContext dbContext,
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        IReadOnlyCollection<ISoftDeleteCascadeRule> softDeleteCascadeRules,
        HashSet<IAuditableTenantEntity> visited,
        CancellationToken cancellationToken
    )
    {
        if (!visited.Add(entity))
            return;

        _stateManager.MarkSoftDeleted(entry, entity, now, actor);

        foreach (
            ISoftDeleteCascadeRule? rule in softDeleteCascadeRules.Where(r => r.CanHandle(entity))
        )
        {
            IReadOnlyCollection<IAuditableTenantEntity> dependents = await rule.GetDependentsAsync(
                dbContext,
                entity,
                cancellationToken
            );
            foreach (IAuditableTenantEntity dependent in dependents)
            {
                if (dependent.IsDeleted || dependent.TenantId != entity.TenantId)
                    continue;

                EntityEntry<IAuditableTenantEntity> dependentEntry = dbContext.Entry(dependent);
                await SoftDeleteWithRulesAsync(
                    dbContext,
                    dependentEntry,
                    dependent,
                    now,
                    actor,
                    softDeleteCascadeRules,
                    visited,
                    cancellationToken
                );
            }
        }
    }
}
