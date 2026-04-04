using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.SoftDelete;

/// <summary>
///     Defines explicit soft-delete cascade behavior for one aggregate/entity type.
/// </summary>
public interface ISoftDeleteCascadeRule
{
    public bool CanHandle(IAuditableTenantEntity entity);

    public Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        DbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    );
}
