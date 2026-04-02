using SharedKernel.Domain.Entities.Contracts;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.SoftDelete;

/// <summary>
/// Defines explicit soft-delete cascade behavior for one aggregate/entity type.
/// </summary>
public interface ISoftDeleteCascadeRule
{
    bool CanHandle(IAuditableTenantEntity entity);

    Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        DbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    );
}
