namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;

/// <summary>
/// Provides leader-election semantics for distributed recurring jobs so that only one
/// application instance executes a given job at a time in a multi-node deployment.
/// </summary>
public interface IDistributedJobCoordinator
{
    /// <summary>
    /// Acquires a distributed lease for <paramref name="jobName"/> and, if successful,
    /// invokes <paramref name="action"/>; otherwise skips execution silently.
    /// The lease is released automatically when <paramref name="action"/> completes or faults.
    /// </summary>
    Task ExecuteIfLeaderAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    );
}
