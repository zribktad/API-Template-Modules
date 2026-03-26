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

public sealed class EmailRetryRecurringJobTests
{
    [Fact]
    public async Task ExecuteAsync_InvokesRetryAndDeadLetterWorkflowInsideCoordinator()
    {
        var ct = TestContext.Current.CancellationToken;
        var retryService = new Mock<IEmailRetryService>();
        var coordinator = new Mock<IDistributedJobCoordinator>();
        var coordinatorCalled = false;

        coordinator
            .Setup(x =>
                x.ExecuteIfLeaderAsync(
                    "email-retry-recurring-job",
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

        var sut = new EmailRetryRecurringJob(
            retryService.Object,
            coordinator.Object,
            Options.Create(
                new BackgroundJobsOptions
                {
                    EmailRetry = new EmailRetryJobOptions
                    {
                        BatchSize = 77,
                        MaxRetryAttempts = 5,
                        DeadLetterAfterHours = 48,
                    },
                }
            ),
            NullLogger<EmailRetryRecurringJob>.Instance
        );

        await sut.ExecuteAsync(new TickerFunctionContext(), ct);

        coordinatorCalled.ShouldBeTrue();
        retryService.Verify(x => x.RetryFailedEmailsAsync(5, 77, ct), Times.Once);
        retryService.Verify(x => x.DeadLetterExpiredAsync(48, 77, ct), Times.Once);
    }
}
