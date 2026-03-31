namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Write-side contract for enqueuing generic background job identifiers (as <see cref="Guid"/>s).
/// </summary>
public interface IJobQueue : IQueue<Guid>;

/// <summary>
/// Read-side contract for consuming job identifiers from the generic job queue.
/// </summary>
public interface IJobQueueReader : IQueueReader<Guid>;
