using Identity.Auth.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     In-process refresh coordination for a single API instance (no Redis). Shared by
///     <see cref="InProcessBffRefreshCoordinator" /> and the fallback path of
///     <see cref="RedisBffRefreshCoordinator" /> when the multiplexer is disconnected.
/// </summary>
internal sealed class InProcessBffRefreshCore
{
    private readonly BffOptions _options;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task<BffRefreshOutcome>> _inFlightRefreshBySessionId = [];

    public InProcessBffRefreshCore(BffOptions options)
    {
        _options = options;
    }

    public async Task<BffRefreshOutcome> ExecuteAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct = default
    )
    {
        Task<BffRefreshOutcome>? existingTask = null;
        Task<BffRefreshOutcome>? leaderTask = null;

        lock (_lock)
        {
            if (
                _inFlightRefreshBySessionId.TryGetValue(
                    sessionId,
                    out Task<BffRefreshOutcome>? inFlightTask
                )
            )
            {
                existingTask = inFlightTask;
            }
            else
            {
                CancellationTokenSource leaderCts = new(
                    TimeSpan.FromMilliseconds(_options.RefreshLockTimeoutMilliseconds)
                );
                leaderTask = leaderAction(leaderCts.Token);
                _ = leaderTask.ContinueWith(_ => leaderCts.Dispose(), TaskScheduler.Default);
                _inFlightRefreshBySessionId[sessionId] = leaderTask;
            }
        }

        if (existingTask is not null)
        {
            BffRefreshOutcome completedOutcome;
            try
            {
                completedOutcome = await existingTask.WaitAsync(
                    TimeSpan.FromMilliseconds(_options.RefreshWaitTimeoutMilliseconds),
                    ct
                );
            }
            catch (TimeoutException)
            {
                return BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected);
            }

            return completedOutcome.Succeeded ? await followerAction(ct) : completedOutcome;
        }

        try
        {
            return await leaderTask!;
        }
        finally
        {
            lock (_lock)
            {
                _inFlightRefreshBySessionId.Remove(sessionId);
            }
        }
    }
}
