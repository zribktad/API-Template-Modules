using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class DragonflyDistributedJobCoordinatorTests
{
    [Fact]
    public async Task ExecuteIfLeaderAsync_ThrowsWhenDragonflyIsUnavailableAndFailClosedIsEnabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(x => x.IsConnected).Returns(false);

        var sut = CreateSut(multiplexer.Object, failClosed: true);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.ExecuteIfLeaderAsync("cleanup", _ => Task.CompletedTask, ct)
        );
    }

    [Fact]
    public async Task ExecuteIfLeaderAsync_SkipsActionWhenAnotherInstanceOwnsLease()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = new Mock<IDatabase>();
        database
            .Setup(x =>
                x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                )
            )
            .ReturnsAsync(false);

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(x => x.IsConnected).Returns(true);
        multiplexer
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var sut = CreateSut(multiplexer.Object, failClosed: true);
        var executed = false;

        await sut.ExecuteIfLeaderAsync(
            "cleanup",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            ct
        );

        executed.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteIfLeaderAsync_WhenLeaseIsAcquired_RunsActionAndReleasesLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = new Mock<IDatabase>();
        database
            .Setup(x =>
                x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                )
            )
            .ReturnsAsync(true);
        database
            .Setup(x =>
                x.ScriptEvaluateAsync(
                    It.IsAny<LuaScript>(),
                    It.IsAny<object>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(RedisResult.Create(1));

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(x => x.IsConnected).Returns(true);
        multiplexer
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var sut = CreateSut(multiplexer.Object, failClosed: true);
        var executed = false;

        await sut.ExecuteIfLeaderAsync(
            "cleanup",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            ct
        );

        executed.ShouldBeTrue();
        database.Verify(
            x =>
                x.ScriptEvaluateAsync(
                    It.IsAny<LuaScript>(),
                    It.IsAny<object>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ExecuteIfLeaderAsync_WhenFailClosedIsDisabled_RunsActionWithoutCoordination()
    {
        var ct = TestContext.Current.CancellationToken;
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(x => x.IsConnected).Returns(false);

        var sut = CreateSut(multiplexer.Object, failClosed: false);
        var executed = false;

        await sut.ExecuteIfLeaderAsync(
            "cleanup",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            ct
        );

        executed.ShouldBeTrue();
    }

    private static DragonflyDistributedJobCoordinator CreateSut(
        IConnectionMultiplexer multiplexer,
        bool failClosed
    ) =>
        new(
            multiplexer,
            Options.Create(
                new BackgroundJobsOptions
                {
                    TickerQ = new TickerQSchedulerOptions { FailClosed = failClosed },
                }
            ),
            NullLogger<DragonflyDistributedJobCoordinator>.Instance
        );
}
