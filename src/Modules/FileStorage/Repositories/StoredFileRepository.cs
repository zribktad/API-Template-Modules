using BuildingBlocks.Infrastructure.EFCore.Repositories;
using FileStorage.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileStorage.Domain;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository
{
    /// <inheritdoc />
    public Task AcquireBlobDeletionLockAsync(
        Guid tenantId,
        string sha256,
        CancellationToken ct = default
    )
    {
        // Advisory locks are a PostgreSQL feature; skip on non-relational providers (e.g. the
        // in-memory provider used by unit tests), which also can't execute raw SQL.
        if (!dbContext.Database.IsNpgsql())
            return Task.CompletedTask;

        // hashtextextended yields a deterministic bigint across processes (unlike Guid.GetHashCode),
        // so concurrent nodes derive the same advisory-lock key for a given (tenant, sha256).
        string key = $"{tenantId:N}:{sha256}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
            ct
        );
    }
}
