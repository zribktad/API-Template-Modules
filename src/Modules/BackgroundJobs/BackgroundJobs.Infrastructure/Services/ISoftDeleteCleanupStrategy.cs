namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Strategy abstraction for purging soft-deleted records of a specific entity type.
/// </summary>
public interface ISoftDeleteCleanupStrategy
{
    string EntityName { get; }
    Task<int> CleanupAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
