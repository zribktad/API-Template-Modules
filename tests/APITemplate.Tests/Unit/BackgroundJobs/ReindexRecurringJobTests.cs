using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TickerQ.Utilities.Base;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class ReindexRecurringJobTests
{
    [Fact]
    public async Task ExecuteAsync_InvokesReindexServiceInsideCoordinator()
    {
        var ct = TestContext.Current.CancellationToken;
        var reindexService = new Mock<IReindexService>();
        var coordinator = new Mock<IDistributedJobCoordinator>();
        var coordinatorCalled = false;

        coordinator
            .Setup(x =>
                x.ExecuteIfLeaderAsync(
                    "reindex-recurring-job",
                    It.IsAny<Func<CancellationToken, Task>>(),
                    ct
                )
            )
            .Returns<string, Func<CancellationToken, Task>, CancellationToken>(
                async (_, action, token) =>
                {
                    coordinatorCalled = true;
                    await action(token);
                }
            );

        var sut = new ReindexRecurringJob(
            reindexService.Object,
            coordinator.Object,
            NullLogger<ReindexRecurringJob>.Instance
        );

        await sut.ExecuteAsync(new TickerFunctionContext(), ct);

        coordinatorCalled.ShouldBeTrue();
        reindexService.Verify(x => x.ReindexFullTextSearchAsync(ct), Times.Once);
    }
}
