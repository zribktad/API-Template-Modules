using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TickerQ.Utilities.Base;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class ExternalSyncRecurringJobTests
{
    [Fact]
    public async Task ExecuteAsync_InvokesSyncServiceInsideCoordinator()
    {
        var ct = TestContext.Current.CancellationToken;
        var syncService = new Mock<IExternalIntegrationSyncService>();
        var coordinator = new Mock<IDistributedJobCoordinator>();
        var coordinatorCalled = false;

        coordinator
            .Setup(x =>
                x.ExecuteIfLeaderAsync(
                    "external-sync-recurring-job",
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

        var sut = new ExternalSyncRecurringJob(
            syncService.Object,
            coordinator.Object,
            NullLogger<ExternalSyncRecurringJob>.Instance
        );

        await sut.ExecuteAsync(new TickerFunctionContext(), ct);

        coordinatorCalled.ShouldBeTrue();
        syncService.Verify(x => x.SynchronizeAsync(ct), Times.Once);
    }
}
