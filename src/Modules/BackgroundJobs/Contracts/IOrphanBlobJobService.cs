namespace BackgroundJobs.Contracts;

/// <summary>
///     Coordinates the FileStorage orphan-blob sweep, dispatching a <c>SweepOrphanBlobsCommand</c> to the
///     FileStorage module via the message bus.
/// </summary>
public interface IOrphanBlobJobService
{
    Task RunSweepAsync(CancellationToken ct);
}
