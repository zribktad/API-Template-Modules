namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Strategy abstraction for purging soft-deleted records of a specific entity type.
/// Implementations are discovered and invoked by <see cref="CleanupService"/> during the scheduled cleanup job.
/// </summary>
public interface ISoftDeleteCleanupStrategy
{
    /// <summary>Gets the name of the entity type this strategy handles, used for logging.</summary>
    string EntityName { get; }

    /// <summary>
    /// Permanently deletes soft-deleted records older than <paramref name="cutoff"/> in batches.
    /// Returns the total number of rows deleted.
    /// </summary>
    Task<int> CleanupAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
