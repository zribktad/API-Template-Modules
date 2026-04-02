using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;

/// <summary>
/// Dragonfly/Redis-backed implementation of <see cref="IDistributedJobCoordinator"/> that uses
/// a SET NX distributed lock with periodic lease renewal to guarantee single-leader job execution.
/// When <c>FailClosed</c> is enabled, any Redis unavailability throws rather than running without coordination.
/// </summary>
public sealed class DragonflyDistributedJobCoordinator : IDistributedJobCoordinator
{
    private const int LeaseSeconds = 300;
    private const double LeaseRenewalDivider = 3.0;
    private static readonly LuaScript RenewLeaseScript = LuaScript.Prepare(
        """
        if redis.call('get', @key) == @value then
            return redis.call('expire', @key, @leaseSeconds)
        end
        return 0
        """
    );
    private static readonly LuaScript ReleaseLockScript = LuaScript.Prepare(
        "if redis.call('get', @key) == @value then return redis.call('del', @key) else return 0 end"
    );

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly BackgroundJobsOptions _options;
    private readonly ILogger<DragonflyDistributedJobCoordinator> _logger;

    public DragonflyDistributedJobCoordinator(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<BackgroundJobsOptions> options,
        ILogger<DragonflyDistributedJobCoordinator> logger
    )
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to acquire a SET NX lock in Dragonfly for <paramref name="jobName"/>, then
    /// runs <paramref name="action"/> while a background timer renews the lease every third of the
    /// lease window; releases the lock unconditionally on completion or failure.
    /// </summary>
    public async Task ExecuteIfLeaderAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    )
    {
        var database = RequireCoordination(jobName);
        if (database is null)
        {
            await action(ct);
            return;
        }

        var lockKey = $"TickerQ:Leader:{_options.TickerQ.InstanceNamePrefix}:{jobName}";
        var lockValue = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

        var acquired = await database.StringSetAsync(
            lockKey,
            lockValue,
            TimeSpan.FromSeconds(LeaseSeconds),
            when: When.NotExists
        );

        if (!acquired)
        {
            _logger.LogDebug(
                "Skipped background job {JobName} because another instance currently owns the coordination lease.",
                jobName
            );
            return;
        }

        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewalTask = RenewLeaseAsync(database, lockKey, lockValue, jobName, executionCts);

        try
        {
            await action(executionCts.Token);
        }
        finally
        {
            executionCts.Cancel();
            await AwaitRenewalAsync(renewalTask);
            await ReleaseAsync(database, lockKey, lockValue);
        }
    }

    /// <summary>
    /// Returns the active Redis <see cref="IDatabase"/>, or <see langword="null"/> when
    /// coordination is unavailable and <c>FailClosed</c> is disabled (fail-open mode).
    /// Throws <see cref="InvalidOperationException"/> when <c>FailClosed</c> is enabled.
    /// </summary>
    private IDatabase? RequireCoordination(string jobName)
    {
        if (!_connectionMultiplexer.IsConnected)
        {
            return HandleUnavailable(jobName, "DragonFly connection is not established.");
        }

        try
        {
            return _connectionMultiplexer.GetDatabase();
        }
        catch (Exception ex)
        {
            return HandleUnavailable(jobName, "DragonFly coordination is unavailable.", ex);
        }
    }

    private IDatabase? HandleUnavailable(
        string jobName,
        string message,
        Exception? innerException = null
    )
    {
        if (!_options.TickerQ.FailClosed)
        {
            _logger.LogWarning(
                innerException,
                "DragonFly coordination is unavailable for background job {JobName}; continuing because fail-closed is disabled. {Message}",
                jobName,
                message
            );
            return null;
        }

        throw CreateFailClosedException(jobName, message, innerException);
    }

    private InvalidOperationException CreateFailClosedException(
        string jobName,
        string message,
        Exception? innerException = null
    )
    {
        _logger.LogWarning(
            innerException,
            "Fail-closed coordination stopped background job {JobName}: {Message}",
            jobName,
            message
        );

        return new InvalidOperationException(
            $"Background job '{jobName}' did not start because DragonFly coordination is unavailable. {message}",
            innerException
        );
    }

    private static async Task AwaitRenewalAsync(Task renewalTask)
    {
        try
        {
            await renewalTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the owner finishes and stops renewing the lease.
        }
    }

    /// <summary>
    /// Runs a periodic loop that extends the lock TTL using an atomic Lua compare-and-expire script.
    /// Cancels <paramref name="executionCts"/> and throws <see cref="LeadershipLeaseLostException"/>
    /// if the renewal fails, indicating another node has taken ownership.
    /// </summary>
    private async Task RenewLeaseAsync(
        IDatabase database,
        string key,
        string value,
        string jobName,
        CancellationTokenSource executionCts
    )
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(LeaseSeconds / LeaseRenewalDivider)
        );
        while (await timer.WaitForNextTickAsync(executionCts.Token))
        {
            var renewed = (long)
                await database.ScriptEvaluateAsync(
                    RenewLeaseScript,
                    new
                    {
                        key,
                        value,
                        leaseSeconds = LeaseSeconds,
                    }
                );

            if (renewed != 0)
            {
                continue;
            }

            _logger.LogWarning(
                "Lost DragonFly coordination lease for background job {JobName}; cancelling the in-flight execution.",
                jobName
            );
            executionCts.Cancel();
            throw new LeadershipLeaseLostException(jobName);
        }
    }

    private static Task ReleaseAsync(IDatabase database, string key, string value) =>
        database.ScriptEvaluateAsync(ReleaseLockScript, new { key, value });

    private sealed class LeadershipLeaseLostException(string jobName)
        : InvalidOperationException(
            $"Background job '{jobName}' lost its DragonFly coordination lease while still running."
        );
}
