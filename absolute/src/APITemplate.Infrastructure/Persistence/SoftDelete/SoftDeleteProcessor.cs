using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.SoftDelete;

/// <summary>
/// Default implementation of <see cref="ISoftDeleteProcessor"/> that recursively soft-deletes
/// an entity and all dependents surfaced by cascade rules, guarding against cycles via a visited set.
/// </summary>
public sealed class SoftDeleteProcessor : ISoftDeleteProcessor
{
    private readonly IAuditableEntityStateManager _stateManager;

    public SoftDeleteProcessor(IAuditableEntityStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    /// <inheritdoc />
    public Task ProcessAsync(
        AppDbContext dbContext,
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
        AppDbContext dbContext,
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

        foreach (var rule in softDeleteCascadeRules.Where(r => r.CanHandle(entity)))
        {
            var dependents = await rule.GetDependentsAsync(dbContext, entity, cancellationToken);
            foreach (var dependent in dependents)
            {
                if (dependent.IsDeleted || dependent.TenantId != entity.TenantId)
                    continue;

                var dependentEntry = dbContext.Entry(dependent);
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
