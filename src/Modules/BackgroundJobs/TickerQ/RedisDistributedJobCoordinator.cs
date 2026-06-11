using BackgroundJobs.Logging;
using BuildingBlocks.Infrastructure.Redis.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BackgroundJobs.TickerQ;

public sealed class RedisDistributedJobCoordinator : IDistributedJobCoordinator
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

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisDistributedJobCoordinator> _logger;
    private readonly BackgroundJobsOptions _options;

    public RedisDistributedJobCoordinator(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<BackgroundJobsOptions> options,
        ILogger<RedisDistributedJobCoordinator> logger
    )
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteIfLeaderAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    )
    {
        IDatabase? database = RequireCoordination(jobName);
        if (database is null)
        {
            await action(ct);
            return;
        }

        string lockKey = $"TickerQ:Leader:{_options.TickerQ.InstanceNamePrefix}:{jobName}";
        string lockValue = RedisLuaScripts.GenerateLockOwnerToken();

        bool acquired = await database.StringSetAsync(
            lockKey,
            lockValue,
            TimeSpan.FromSeconds(LeaseSeconds),
            When.NotExists
        );

        if (!acquired)
        {
            _logger.JobSkippedLeadershipNotAcquired(jobName);
            return;
        }

        using CancellationTokenSource executionCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task renewalTask = RenewLeaseAsync(database, lockKey, lockValue, jobName, executionCts);

        try
        {
            await action(executionCts.Token);
        }
        finally
        {
            executionCts.Cancel();
            await AwaitRenewalAsync(renewalTask, jobName);
            await ReleaseAsync(database, lockKey, lockValue);
        }
    }

    private IDatabase? RequireCoordination(string jobName)
    {
        if (!_connectionMultiplexer.IsConnected)
            return HandleUnavailable(jobName, "Redis connection is not established.");

        try
        {
            return _connectionMultiplexer.GetDatabase();
        }
        catch (Exception ex)
        {
            return HandleUnavailable(jobName, "Redis coordination is unavailable.", ex);
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
            _logger.CoordinationUnavailableFailOpenContinuing(innerException, jobName, message);
            return null;
        }

        _logger.CoordinationFailClosedStopped(innerException, jobName, message);

        throw new InvalidOperationException(
            $"Background job '{jobName}' did not start because Redis coordination is unavailable. {message}",
            innerException
        );
    }

    private async Task AwaitRenewalAsync(Task renewalTask, string jobName)
    {
        try
        {
            await renewalTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.CoordinationLeaseRenewalError(ex, jobName);
        }
    }

    private async Task RenewLeaseAsync(
        IDatabase database,
        string key,
        string value,
        string jobName,
        CancellationTokenSource executionCts
    )
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(LeaseSeconds / LeaseRenewalDivider));
        while (await timer.WaitForNextTickAsync(executionCts.Token))
        {
            long renewed;
            try
            {
                renewed = (long)
                    await database.ScriptEvaluateAsync(
                        RenewLeaseScript,
                        new
                        {
                            key,
                            value,
                            leaseSeconds = LeaseSeconds,
                        }
                    );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A Redis outage means we can no longer prove we hold the lease. Fail safe: cancel
                // the in-flight execution rather than letting it run unprotected until the lease
                // expires and another node acquires it (split-brain).
                _logger.CoordinationLeaseRenewalError(ex, jobName);
                executionCts.Cancel();
                throw new InvalidOperationException(
                    $"Background job '{jobName}' could not renew its Redis coordination lease.",
                    ex
                );
            }

            if (renewed != 0)
                continue;

            _logger.CoordinationLeaseLost(jobName);
            executionCts.Cancel();
            throw new InvalidOperationException(
                $"Background job '{jobName}' lost its Redis coordination lease while still running."
            );
        }
    }

    private static Task ReleaseAsync(IDatabase database, string key, string value)
    {
        return database.ScriptEvaluateAsync(RedisLuaScripts.ReleaseLock, new { key, value });
    }
}
