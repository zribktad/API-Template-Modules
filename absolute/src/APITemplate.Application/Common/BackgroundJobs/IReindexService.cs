namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Application-layer contract for rebuilding full-text search indexes.
/// Implementations are provided by the Infrastructure layer and scheduled as recurring background jobs.
/// </summary>
public interface IReindexService
{
    /// <summary>
    /// Triggers a full rebuild of the full-text search index for all indexed entities.
    /// </summary>
    Task ReindexFullTextSearchAsync(CancellationToken ct = default);
}
