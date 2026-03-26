using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using TickerQ.Utilities.Base;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class CleanupRecurringJobTests
{
    [Fact]
    public async Task ExecuteAsync_InvokesCleanupWorkflowInsideCoordinator()
    {
        var ct = TestContext.Current.CancellationToken;
        var cleanupService = new Mock<ICleanupService>();
        var coordinator = new Mock<IDistributedJobCoordinator>();
        var coordinatorCalled = false;

        coordinator
            .Setup(x =>
                x.ExecuteIfLeaderAsync(
                    "cleanup-recurring-job",
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

        var sut = new CleanupRecurringJob(
            cleanupService.Object,
            coordinator.Object,
            Options.Create(
                new BackgroundJobsOptions
                {
                    Cleanup = new CleanupJobOptions
                    {
                        BatchSize = 123,
                        ExpiredInvitationRetentionHours = 10,
                        SoftDeleteRetentionDays = 20,
                        OrphanedProductDataRetentionDays = 30,
                    },
                }
            ),
            NullLogger<CleanupRecurringJob>.Instance
        );

        await sut.ExecuteAsync(new TickerFunctionContext(), ct);

        coordinatorCalled.ShouldBeTrue();
        cleanupService.Verify(x => x.CleanupExpiredInvitationsAsync(10, 123, ct), Times.Once);
        cleanupService.Verify(x => x.CleanupSoftDeletedRecordsAsync(20, 123, ct), Times.Once);
        cleanupService.Verify(x => x.CleanupOrphanedProductDataAsync(30, 123, ct), Times.Once);
    }
}
