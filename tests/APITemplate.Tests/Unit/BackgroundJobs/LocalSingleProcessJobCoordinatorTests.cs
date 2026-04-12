using BackgroundJobs.TickerQ;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class LocalSingleProcessJobCoordinatorTests
{
    [Fact]
    public async Task ExecuteIfLeaderAsync_AlwaysRunsAction()
    {
        var coordinator = new LocalSingleProcessJobCoordinator();
        var ct = TestContext.Current.CancellationToken;
        int calls = 0;

        await coordinator.ExecuteIfLeaderAsync(
            "test-job",
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            ct
        );

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteIfLeaderAsync_PropagatesCancellation()
    {
        var coordinator = new LocalSingleProcessJobCoordinator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            coordinator.ExecuteIfLeaderAsync(
                "test-job",
                ct => Task.Delay(Timeout.Infinite, ct),
                cts.Token
            )
        );
    }
}
