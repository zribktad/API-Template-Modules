using System.Text.Json;
using BuildingBlocks.Infrastructure.Redis.Redis;
using Identity.Auth.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Redis-backed refresh coordinator that serializes concurrent refresh attempts per session and
///     shares the leader outcome with waiting followers.
/// </summary>
public sealed class RedisBffRefreshCoordinator : IBffRefreshCoordinator
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly BffOptions _options;
    private readonly InProcessBffRefreshCore _inProcessCore;

    public RedisBffRefreshCoordinator(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<BffOptions> options
    )
    {
        _multiplexer = connectionMultiplexer;
        _options = options.Value;
        _inProcessCore = new InProcessBffRefreshCore(_options);
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
            return await _inProcessCore.ExecuteAsync(sessionId, leaderAction, followerAction, ct);

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
            // Leader CTS matches lock TTL so refresh can finish and publish outcome even if the HTTP client disconnects.
            using CancellationTokenSource leaderCts = new(
                TimeSpan.FromMilliseconds(_options.RefreshLockTimeoutMilliseconds)
            );

            try
            {
                BffRefreshOutcome leaderOutcome = await leaderAction(leaderCts.Token);
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

    private static string GetLockKey(string sessionId) =>
        $"{BffSessionCacheKeys.SessionKeyPrefix}{sessionId}:refresh:lock";

    private static string GetResultKey(string sessionId) =>
        $"{BffSessionCacheKeys.SessionKeyPrefix}{sessionId}:refresh:result";

    private static RedisChannel GetNotifyChannel(string sessionId) =>
        RedisChannel.Literal($"{BffSessionCacheKeys.SessionKeyPrefix}{sessionId}:refresh:notify");

    private sealed record BffRefreshCoordinatorPayload(
        bool Succeeded,
        BffSessionRevocationReason? FailureReason
    );
}
