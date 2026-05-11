namespace BuildingBlocks.Application.BackgroundJobs;

/// <summary>
///     Coordinates execution of background jobs across multiple application instances,
///     ensuring that only the current leader node executes the provided action.
/// </summary>
public interface IDistributedJobCoordinator
{
    /// <summary>
    ///     Executes <paramref name="action" /> only if the current node holds the leader lease for <paramref name="jobName" />
    ///     .
    ///     If the current node is not the leader, the action is skipped without error.
    /// </summary>
    Task ExecuteIfLeaderAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    );
}
