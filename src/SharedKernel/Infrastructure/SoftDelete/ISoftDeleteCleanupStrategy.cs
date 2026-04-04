namespace SharedKernel.Infrastructure.SoftDelete;

/// <summary>
///     Defines a strategy for permanently removing soft-deleted records of a specific entity type
///     that have exceeded the configured retention window.
/// </summary>
public interface ISoftDeleteCleanupStrategy
{
    public string EntityName { get; }

    public Task<int> CleanupAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
