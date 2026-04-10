using System.Text.Json;
using Identity.Options;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Redis;
using StackExchange.Redis;

namespace Identity.Security.Sessions;

/// <summary>
///     Redis-backed refresh coordinator that serializes concurrent refresh attempts per session and
///     shares the leader outcome with waiting followers.
/// </summary>
public sealed class DragonflyBffRefreshCoordinator : IBffRefreshCoordinator
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly BffOptions _options;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task<BffRefreshOutcome>> _fallbackTasks = [];

    public DragonflyBffRefreshCoordinator(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<BffOptions> options
    )
    {
        _multiplexer = connectionMultiplexer;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<BffRefreshOutcome> ExecuteAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct = default
    )
    {
        if (!_multiplexer.IsConnected)
            return await ExecuteWithFallbackSemaphoreAsync(
                sessionId,
                leaderAction,
                followerAction,
                ct
            );

        IDatabase database = _multiplexer.GetDatabase();
        string lockKey = GetLockKey(sessionId);
        string lockValue = RedisLuaScripts.GenerateLockOwnerToken();

        bool acquired = await database.StringSetAsync(
            lockKey,
            lockValue,
            TimeSpan.FromMilliseconds(_options.RefreshLockTimeoutMilliseconds),
            When.NotExists
        );

        if (acquired)
        {
            try
            {
                BffRefreshOutcome leaderOutcome = await leaderAction(ct);
                await WriteOutcomeAsync(database, sessionId, leaderOutcome);
                return leaderOutcome;
            }
            finally
            {
                await database.ScriptEvaluateAsync(
                    RedisLuaScripts.ReleaseLock,
                    new { key = lockKey, value = lockValue }
                );
            }
        }

        return await WaitForLeaderOutcomeAsync(database, sessionId, followerAction, ct);
    }

    private async Task<BffRefreshOutcome> ExecuteWithFallbackSemaphoreAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct
    )
    {
        Task<BffRefreshOutcome>? existingTask = null;
        Task<BffRefreshOutcome>? leaderTask = null;

        lock (_lock)
        {
            if (_fallbackTasks.TryGetValue(sessionId, out Task<BffRefreshOutcome>? inFlightTask))
            {
                existingTask = inFlightTask;
            }
            else
            {
                leaderTask = leaderAction(ct);
                _fallbackTasks[sessionId] = leaderTask;
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
                _fallbackTasks.Remove(sessionId);
            }
        }
    }

    private async Task<BffRefreshOutcome?> TryReadOutcomeAsync(
        IDatabase database,
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct
    )
    {
        RedisValue outcomePayload = await database.StringGetAsync(GetResultKey(sessionId));
        if (!outcomePayload.HasValue)
            return null;

        BffRefreshCoordinatorPayload? payload =
            JsonSerializer.Deserialize<BffRefreshCoordinatorPayload>(
                outcomePayload.ToString(),
                BffSessionSerializerOptions.Instance
            );

        if (payload is null)
            return null;

        return payload.Succeeded
            ? await followerAction(ct)
            : BffRefreshOutcome.Failed(
                payload.FailureReason ?? BffSessionRevocationReason.RefreshRejected
            );
    }

    private async Task<BffRefreshOutcome> WaitForLeaderOutcomeAsync(
        IDatabase database,
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct
    )
    {
        ISubscriber subscriber = _multiplexer.GetSubscriber();
        RedisChannel channel = GetNotifyChannel(sessionId);
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await subscriber.SubscribeAsync(channel, (_, _) => tcs.TrySetResult(true));
        try
        {
            BffRefreshOutcome? earlyResult = await TryReadOutcomeAsync(
                database,
                sessionId,
                followerAction,
                ct
            );
            if (earlyResult is not null)
                return earlyResult;

            using CancellationTokenSource timeoutCts = new(_options.RefreshWaitTimeoutMilliseconds);
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

            try
            {
                await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout or caller cancellation — fall through to final GET attempt.
            }

            BffRefreshOutcome? finalResult = await TryReadOutcomeAsync(
                database,
                sessionId,
                followerAction,
                ct
            );
            return finalResult
                ?? BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected);
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel);
        }
    }

    private async Task WriteOutcomeAsync(
        IDatabase database,
        string sessionId,
        BffRefreshOutcome outcome
    )
    {
        BffRefreshCoordinatorPayload payload = new(outcome.Succeeded, outcome.FailureReason);
        string json = JsonSerializer.Serialize(payload, BffSessionSerializerOptions.Instance);
        await database.StringSetAsync(
            GetResultKey(sessionId),
            json,
            TimeSpan.FromMilliseconds(_options.RefreshResultTtlMilliseconds)
        );

        ISubscriber subscriber = _multiplexer.GetSubscriber();
        await subscriber.PublishAsync(GetNotifyChannel(sessionId), "done");
    }

    private static string GetLockKey(string sessionId) => $"bff:session:{sessionId}:refresh:lock";

    private static string GetResultKey(string sessionId) =>
        $"bff:session:{sessionId}:refresh:result";

    private static RedisChannel GetNotifyChannel(string sessionId) =>
        RedisChannel.Literal($"bff:session:{sessionId}:refresh:notify");

    private sealed record BffRefreshCoordinatorPayload(
        bool Succeeded,
        BffSessionRevocationReason? FailureReason
    );
}
