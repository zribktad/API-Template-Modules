using System.Text.Json;
using Identity.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Security.Sessions;

/// <summary>
///     Redis-backed refresh coordinator that serializes concurrent refresh attempts per session and
///     shares the leader outcome with waiting followers.
/// </summary>
public sealed class DragonflyBffRefreshCoordinator : IBffRefreshCoordinator
{
    private static readonly LuaScript ReleaseLockScript = LuaScript.Prepare(
        "if redis.call('get', @key) == @value then return redis.call('del', @key) else return 0 end"
    );

    private readonly IConnectionMultiplexer? _multiplexer;
    private readonly BffOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private readonly Dictionary<string, Task<BffRefreshOutcome>> _fallbackTasks = [];

    public DragonflyBffRefreshCoordinator(
        IEnumerable<IConnectionMultiplexer> connectionMultiplexers,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
    {
        _multiplexer = connectionMultiplexers.FirstOrDefault();
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<BffRefreshOutcome> ExecuteAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct = default
    )
    {
        if (_multiplexer is null || !_multiplexer.IsConnected)
            return await ExecuteWithFallbackSemaphoreAsync(
                sessionId,
                leaderAction,
                followerAction,
                ct
            );

        IDatabase database = _multiplexer.GetDatabase();
        string lockKey = GetLockKey(sessionId);
        string lockValue = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

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
                    ReleaseLockScript,
                    new { key = lockKey, value = lockValue }
                );
            }
        }

        DateTimeOffset deadline = _timeProvider
            .GetUtcNow()
            .AddMilliseconds(_options.RefreshWaitTimeoutMilliseconds);

        while (_timeProvider.GetUtcNow() < deadline)
        {
            RedisValue outcomePayload = await database.StringGetAsync(GetResultKey(sessionId));
            if (outcomePayload.HasValue)
            {
                BffRefreshCoordinatorPayload? payload =
                    JsonSerializer.Deserialize<BffRefreshCoordinatorPayload>(
                        outcomePayload.ToString(),
                        DragonflyBffSessionStore.SerializerOptions
                    );
                if (payload is not null)
                    return payload.Succeeded
                        ? await followerAction(ct)
                        : BffRefreshOutcome.Failed(
                            payload.FailureReason ?? BffSessionRevocationReason.RefreshRejected
                        );
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        return BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected);
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

    private async Task WriteOutcomeAsync(
        IDatabase database,
        string sessionId,
        BffRefreshOutcome outcome
    )
    {
        BffRefreshCoordinatorPayload payload = new(outcome.Succeeded, outcome.FailureReason);
        string json = JsonSerializer.Serialize(payload, DragonflyBffSessionStore.SerializerOptions);
        await database.StringSetAsync(
            GetResultKey(sessionId),
            json,
            TimeSpan.FromMilliseconds(_options.RefreshResultTtlMilliseconds)
        );
    }

    private static string GetLockKey(string sessionId) => $"bff:session:{sessionId}:refresh:lock";

    private static string GetResultKey(string sessionId) =>
        $"bff:session:{sessionId}:refresh:result";

    private sealed record BffRefreshCoordinatorPayload(
        bool Succeeded,
        BffSessionRevocationReason? FailureReason
    );
}
