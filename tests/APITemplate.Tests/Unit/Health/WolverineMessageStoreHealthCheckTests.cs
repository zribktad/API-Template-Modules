using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel.Infrastructure.Health;
using Shouldly;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

[Trait("Category", "Unit")]
public sealed class WolverineMessageStoreHealthCheckTests
{
    private readonly Mock<IMessageStore> _messageStore = new();
    private readonly Mock<IMessageStoreAdmin> _admin = new();
    private readonly WolverineHealthCheckOptions _options = new();

    private WolverineMessageStoreHealthCheck CreateSut()
    {
        _messageStore.Setup(s => s.Admin).Returns(_admin.Object);
        return new WolverineMessageStoreHealthCheck(_messageStore.Object, Options.Create(_options));
    }

    [Fact]
    public async Task Returns_Healthy_When_Connected_And_Backlogs_Below_Thresholds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(
                new PersistedCounts
                {
                    Incoming = 0,
                    Outgoing = 0,
                    Scheduled = 5,
                    Handled = 10,
                }
            );

        WolverineMessageStoreHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ShouldContainKey("incoming");
        result.Data.ShouldContainKey("outgoing");
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Connectivity_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        WolverineMessageStoreHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not reachable");
    }

    [Fact]
    public async Task Returns_Degraded_When_Outgoing_Backlog_Exceeds_Threshold()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { Outgoing = 100, Incoming = 0 });

        WolverineMessageStoreHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("backlog");
    }

    [Fact]
    public async Task Returns_Degraded_When_Incoming_Backlog_Exceeds_Threshold()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { Incoming = 100, Outgoing = 0 });

        WolverineMessageStoreHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("backlog");
    }

    [Fact]
    public async Task Data_Dictionary_Contains_All_Counts_On_Healthy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(
                new PersistedCounts
                {
                    Incoming = 3,
                    Outgoing = 7,
                    Scheduled = 2,
                    Handled = 15,
                }
            );

        WolverineMessageStoreHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Data["incoming"].ShouldBe(3);
        result.Data["outgoing"].ShouldBe(7);
        result.Data["scheduled"].ShouldBe(2);
        result.Data["handled"].ShouldBe(15);
    }
}
