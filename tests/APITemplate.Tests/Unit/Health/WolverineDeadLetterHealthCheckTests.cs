using BuildingBlocks.Web.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

[Trait("Category", "Unit")]
public sealed class WolverineDeadLetterHealthCheckTests
{
    private readonly Mock<IMessageStore> _messageStore = new();
    private readonly Mock<IMessageStoreAdmin> _admin = new();
    private readonly WolverineHealthCheckOptions _options = new();

    private WolverineDeadLetterHealthCheck CreateSut()
    {
        _messageStore.Setup(s => s.Admin).Returns(_admin.Object);
        return new WolverineDeadLetterHealthCheck(_messageStore.Object, Options.Create(_options));
    }

    [Fact]
    public async Task Returns_Healthy_When_Dead_Letter_Count_Is_Zero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { DeadLetter = 0 });

        WolverineDeadLetterHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Returns_Degraded_When_Count_Exceeds_Warning_Threshold()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { DeadLetter = 50 });

        WolverineDeadLetterHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("warning");
    }

    [Fact]
    public async Task Returns_Unhealthy_When_Count_Exceeds_Critical_Threshold()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { DeadLetter = 200 });

        WolverineDeadLetterHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("critical");
    }

    [Fact]
    public async Task Returns_Unhealthy_When_FetchCountsAsync_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        WolverineDeadLetterHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Data_Dictionary_Contains_DeadLetterCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _admin
            .Setup(a => a.FetchCountsAsync())
            .ReturnsAsync(new PersistedCounts { DeadLetter = 25 });

        WolverineDeadLetterHealthCheck sut = CreateSut();
        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Data.ShouldContainKey("deadLetterCount");
        result.Data["deadLetterCount"].ShouldBe(25);
    }
}
