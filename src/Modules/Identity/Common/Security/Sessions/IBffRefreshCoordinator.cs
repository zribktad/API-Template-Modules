namespace Identity.Security.Sessions;

/// <summary>
///     Coordinates concurrent refresh attempts so only one request performs the upstream refresh
///     while others observe or reuse the result.
/// </summary>
public interface IBffRefreshCoordinator
{
    /// <summary>
    ///     Executes the provided leader or follower actions for the given session depending on
    ///     whether the caller acquired refresh leadership.
    /// </summary>
    Task<BffRefreshOutcome> ExecuteAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct = default
    );
}
