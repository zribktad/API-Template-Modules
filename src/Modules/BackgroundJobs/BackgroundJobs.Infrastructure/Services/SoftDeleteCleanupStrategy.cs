using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain.Entities.Contracts;

namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Generic implementation of <see cref="ISoftDeleteCleanupStrategy"/> that hard-deletes
/// soft-deleted <typeparamref name="TEntity"/> rows in batches using EF Core bulk-delete.
/// </summary>
public sealed class SoftDeleteCleanupStrategy<TEntity> : ISoftDeleteCleanupStrategy
    where TEntity : class, ISoftDeletable
{
    private readonly DbContext _dbContext;

    public SoftDeleteCleanupStrategy(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string EntityName => typeof(TEntity).Name;

    public async Task<int> CleanupAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken ct = default
    )
    {
        int totalDeleted = 0;
        int deleted;

        do
        {
            deleted = await _dbContext
                .Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(e => e.IsDeleted && e.DeletedAtUtc < cutoff)
                .OrderBy(e => e.DeletedAtUtc)
                .Take(batchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
        } while (deleted == batchSize);

        return totalDeleted;
    }
}
