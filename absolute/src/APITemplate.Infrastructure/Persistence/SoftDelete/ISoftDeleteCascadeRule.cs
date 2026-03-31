using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Persistence.SoftDelete;

/// <summary>
/// Defines explicit soft-delete cascade behavior for one aggregate/entity type.
/// Implementations decide:
/// <list type="bullet">
/// <item><description>which entity types they can handle</description></item>
/// <item><description>which dependents should be soft-deleted together with the root entity</description></item>
/// </list>
/// </summary>
public interface ISoftDeleteCascadeRule
{
    /// <summary>
    /// Returns <c>true</c> when this rule can provide dependents for the given entity instance.
    /// </summary>
    bool CanHandle(IAuditableTenantEntity entity);

    /// <summary>
    /// Returns dependents that should be soft-deleted when the root entity is deleted.
    /// Returned entities must be tracked/auditable entities.
    /// </summary>
    Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        AppDbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default);
}
