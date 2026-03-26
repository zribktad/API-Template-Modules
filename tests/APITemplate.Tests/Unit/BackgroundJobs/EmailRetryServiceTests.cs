using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly.Registry;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class EmailRetryServiceTests
{
    [Fact]
    public async Task RetryFailedEmailsAsync_ClaimsBatchAndClearsClaimOnFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new Mock<IFailedEmailRepository>();
        var sender = new Mock<IEmailSender>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var now = DateTimeOffset.Parse("2026-03-16T20:00:00Z");
        var timeProvider = new FakeTimeProvider(now);

        var email = new FailedEmail
        {
            Id = Guid.NewGuid(),
            To = "user@example.com",
            Subject = "Subject",
            HtmlBody = "<p>Body</p>",
            RetryCount = 0,
            CreatedAtUtc = now.UtcDateTime.AddHours(-1),
            ClaimedBy = "owner",
            ClaimedAtUtc = now.UtcDateTime,
            ClaimedUntilUtc = now.UtcDateTime.AddMinutes(7),
        };

        repository
            .Setup(x =>
                x.ClaimRetryableBatchAsync(
                    5,
                    10,
                    It.IsAny<string>(),
                    now.UtcDateTime,
                    now.UtcDateTime.AddMinutes(7),
                    ct
                )
            )
            .ReturnsAsync([email]);

        sender
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(new string('x', FailedEmail.LastErrorMaxLength + 25))
            );

        var sut = new EmailRetryService(
            repository.Object,
            sender.Object,
            unitOfWork.Object,
            timeProvider,
            Options.Create(
                new BackgroundJobsOptions
                {
                    EmailRetry = new EmailRetryJobOptions { ClaimLeaseMinutes = 7 },
                }
            ),
            CreateRegistry(),
            NullLogger<EmailRetryService>.Instance
        );

        await sut.RetryFailedEmailsAsync(5, 10, ct);

        repository.VerifyAll();
        repository.Verify(x => x.UpdateAsync(email, ct), Times.Once);
        email.RetryCount.ShouldBe(1);
        email.LastError.ShouldNotBeNull();
        email.LastError.Length.ShouldBe(FailedEmail.LastErrorMaxLength);
        email.ClaimedBy.ShouldBeNull();
        email.ClaimedAtUtc.ShouldBeNull();
        email.ClaimedUntilUtc.ShouldBeNull();
        unitOfWork.Verify(x => x.CommitAsync(ct), Times.Once);
    }

    [Fact]
    public async Task DeadLetterExpiredAsync_ClaimsExpiredBatchAndClearsClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new Mock<IFailedEmailRepository>();
        var sender = new Mock<IEmailSender>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var now = DateTimeOffset.Parse("2026-03-16T20:00:00Z");
        var timeProvider = new FakeTimeProvider(now);

        var email = new FailedEmail
        {
            Id = Guid.NewGuid(),
            To = "user@example.com",
            Subject = "Subject",
            HtmlBody = "<p>Body</p>",
            RetryCount = 2,
            CreatedAtUtc = now.UtcDateTime.AddDays(-5),
            ClaimedBy = "owner",
            ClaimedAtUtc = now.UtcDateTime,
            ClaimedUntilUtc = now.UtcDateTime.AddMinutes(4),
        };

        repository
            .SetupSequence(x =>
                x.ClaimExpiredBatchAsync(
                    now.UtcDateTime.AddHours(-48),
                    10,
                    It.IsAny<string>(),
                    now.UtcDateTime,
                    now.UtcDateTime.AddMinutes(4),
                    ct
                )
            )
            .ReturnsAsync([email])
            .ReturnsAsync([]);

        var sut = new EmailRetryService(
            repository.Object,
            sender.Object,
            unitOfWork.Object,
            timeProvider,
            Options.Create(
                new BackgroundJobsOptions
                {
                    EmailRetry = new EmailRetryJobOptions { ClaimLeaseMinutes = 4 },
                }
            ),
            CreateRegistry(),
            NullLogger<EmailRetryService>.Instance
        );

        await sut.DeadLetterExpiredAsync(48, 10, ct);

        repository.Verify(x => x.UpdateAsync(email, ct), Times.Once);
        email.IsDeadLettered.ShouldBeTrue();
        email.ClaimedBy.ShouldBeNull();
        email.ClaimedAtUtc.ShouldBeNull();
        email.ClaimedUntilUtc.ShouldBeNull();
        unitOfWork.Verify(x => x.CommitAsync(ct), Times.Once);
    }

    private static ResiliencePipelineProvider<string> CreateRegistry()
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder(ResiliencePipelineKeys.SmtpSend, (_, _) => { });
        return registry;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
