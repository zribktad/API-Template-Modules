using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Email;

public sealed class FailedEmailStoreTests
{
    [Fact]
    public async Task StoreFailedAsync_TruncatesLastErrorBeforePersisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new Mock<IFailedEmailRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        FailedEmail? storedEmail = null;

        repository
            .Setup(x => x.AddAsync(It.IsAny<FailedEmail>(), ct))
            .Callback<FailedEmail, CancellationToken>((email, _) => storedEmail = email)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        services.AddSingleton(unitOfWork.Object);
        services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(DateTimeOffset.Parse("2026-03-18T10:00:00Z"))
        );

        using var provider = services.BuildServiceProvider();
        var sut = new FailedEmailStore(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new BackgroundJobsOptions
                {
                    EmailRetry = new EmailRetryJobOptions { Enabled = true },
                }
            ),
            NullLogger<FailedEmailStore>.Instance
        );

        await sut.StoreFailedAsync(
            new EmailMessage(
                "user@example.com",
                "Subject",
                "<p>Body</p>",
                "template",
                Retryable: true
            ),
            new string('x', FailedEmail.LastErrorMaxLength + 25),
            ct
        );

        storedEmail.ShouldNotBeNull();
        storedEmail.LastError.ShouldNotBeNull();
        storedEmail.LastError.Length.ShouldBe(FailedEmail.LastErrorMaxLength);
        unitOfWork.Verify(x => x.CommitAsync(ct), Times.Once);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
