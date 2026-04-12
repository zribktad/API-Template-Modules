using SharedKernel.Application.BackgroundJobs;

namespace BackgroundJobs.TickerQ;

/// <summary>
///     Runs recurring job actions on every API instance without Redis-backed coordination.
///     Safe for single-instance deployments; with multiple replicas the same job may execute in parallel.
/// </summary>
public sealed class LocalSingleProcessJobCoordinator : IDistributedJobCoordinator
{
    /// <inheritdoc />
    /// <param name="jobName">Unused; retained for <see cref="IDistributedJobCoordinator" /> contract.</param>
    public Task ExecuteIfLeaderAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    ) => action(ct);
}
