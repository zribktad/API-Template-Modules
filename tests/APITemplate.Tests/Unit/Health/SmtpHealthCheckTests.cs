using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Notifications.Contracts;
using Notifications.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

[Trait("Category", "Unit")]
public sealed class SmtpHealthCheckTests
{
    private readonly Mock<ISmtpClient> _smtpClient = new();

    private readonly EmailOptions _emailOptions = new()
    {
        SmtpHost = "smtp.example.com",
        SmtpPort = 587,
        UseSsl = true,
    };

    private SmtpHealthCheck CreateSut()
    {
        return new SmtpHealthCheck(() => _smtpClient.Object, Options.Create(_emailOptions));
    }

    [Fact]
    public async Task Returns_Healthy_When_Connect_And_Disconnect_Succeed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _smtpClient
            .Setup(c =>
                c.ConnectAsync(
                    _emailOptions.SmtpHost,
                    _emailOptions.SmtpPort,
                    SecureSocketOptions.SslOnConnect,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        _smtpClient
            .Setup(c => c.DisconnectAsync(true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SmtpHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Connect_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _smtpClient
            .Setup(c =>
                c.ConnectAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<SecureSocketOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        SmtpHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not reachable");
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Timeout_Expires()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _smtpClient
            .Setup(c =>
                c.ConnectAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<SecureSocketOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException("Timeout"));

        SmtpHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not reachable");
    }
}
