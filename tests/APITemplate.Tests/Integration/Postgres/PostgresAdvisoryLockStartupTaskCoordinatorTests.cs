using APITemplate.Application.Common.Startup;
using APITemplate.Infrastructure.Persistence.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresAdvisoryLockStartupTaskCoordinatorTests(
    SharedPostgresContainer postgres
) : PostgresTestBase(postgres)
{
    [Fact]
    public async Task CoordinateAsync_WhenCalledConcurrently_SerializesExecution()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbContext1 = await CreateDbContextAsync(false, Guid.Empty, Guid.Empty, ct);
        await using var dbContext2 = await CreateDbContextAsync(false, Guid.Empty, Guid.Empty, ct);

        var coordinator1 = new PostgresAdvisoryLockStartupTaskCoordinator(
            dbContext1,
            NullLogger<PostgresAdvisoryLockStartupTaskCoordinator>.Instance
        );
        var coordinator2 = new PostgresAdvisoryLockStartupTaskCoordinator(
            dbContext2,
            NullLogger<PostgresAdvisoryLockStartupTaskCoordinator>.Instance
        );

        var firstEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var secondEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;

        var firstTask = Task.Run(
            async () =>
            {
                await using var startupLease = await coordinator1.AcquireAsync(
                    StartupTaskName.AppBootstrap,
                    ct
                );
                TrackEnter(ref concurrentExecutions, ref maxConcurrentExecutions);
                firstEntered.TrySetResult();

                try
                {
                    await releaseFirst.Task.WaitAsync(ct);
                }
                finally
                {
                    Interlocked.Decrement(ref concurrentExecutions);
                }
            },
            ct
        );

        await firstEntered.Task.WaitAsync(ct);

        var secondTask = Task.Run(
            async () =>
            {
                await using var startupLease = await coordinator2.AcquireAsync(
                    StartupTaskName.AppBootstrap,
                    ct
                );
                TrackEnter(ref concurrentExecutions, ref maxConcurrentExecutions);
                secondEntered.TrySetResult();

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                }
                finally
                {
                    Interlocked.Decrement(ref concurrentExecutions);
                }
            },
            ct
        );

        await Task.Delay(TimeSpan.FromMilliseconds(100), ct);

        secondEntered.Task.IsCompleted.ShouldBeFalse();
        maxConcurrentExecutions.ShouldBe(1);

        releaseFirst.TrySetResult();

        await Task.WhenAll(firstTask, secondTask);

        secondEntered.Task.IsCompleted.ShouldBeTrue();
        maxConcurrentExecutions.ShouldBe(1);
    }

    private static void TrackEnter(ref int concurrentExecutions, ref int maxConcurrentExecutions)
    {
        var current = Interlocked.Increment(ref concurrentExecutions);

        while (true)
        {
            var snapshot = maxConcurrentExecutions;
            if (snapshot >= current)
            {
                return;
            }

            if (
                Interlocked.CompareExchange(ref maxConcurrentExecutions, current, snapshot)
                == snapshot
            )
            {
                return;
            }
        }
    }
}
