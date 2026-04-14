using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.Persistence;

public static class SoftDeleteQueryableExtensions
{
    /// <summary>
    ///     Applies a bulk soft-delete <c>ExecuteUpdateAsync</c> to the given queryable, setting
    ///     <see cref="ISoftDeletable"/> and <see cref="IAuditableEntity"/> fields in a single
    ///     server-side UPDATE statement. The caller is responsible for filtering the queryable
    ///     (e.g. by tenant, by product IDs) before calling this method.
    /// </summary>
    public static Task<int> BulkSoftDeleteAsync<T>(
        this IQueryable<T> query,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
        where T : class, ISoftDeletable, IAuditableEntity
    {
        return query.ExecuteUpdateAsync(
            setters =>
                setters
                    .SetProperty(e => e.IsDeleted, true)
                    .SetProperty(e => e.DeletedAtUtc, deletedAtUtc)
                    .SetProperty(e => e.DeletedBy, actorId)
                    .SetProperty(e => e.Audit.UpdatedAtUtc, deletedAtUtc)
                    .SetProperty(e => e.Audit.UpdatedBy, actorId),
            ct
        );
    }
}
