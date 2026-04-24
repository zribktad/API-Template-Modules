using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class RedisBffSessionRevocationNotifierTests
{
    private static readonly RedisChannel Channel = RedisChannel.Literal("bff:session:revocations");

    [Fact]
    public async Task PublishRevokedAsync_WhenConnected_PublishesToBroadcastChannel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.SetupGet(x => x.IsConnected).Returns(true);
        multiplexer.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        RedisBffSessionRevocationNotifier sut = new(
            multiplexer.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await sut.PublishRevokedAsync("sess-1", ct);

        subscriber.Verify(
            s => s.PublishAsync(Channel, "sess-1", It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishRevokedAsync_WhenDisconnected_IsNoOp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.SetupGet(x => x.IsConnected).Returns(false);
        multiplexer.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        RedisBffSessionRevocationNotifier sut = new(
            multiplexer.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await sut.PublishRevokedAsync("sess-1", ct);

        subscriber.Verify(
            s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PublishRevokedAsync_SwallowsRedisException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<ISubscriber> subscriber = new();
        subscriber
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ThrowsAsync(
                new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom")
            );
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.SetupGet(x => x.IsConnected).Returns(true);
        multiplexer.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        RedisBffSessionRevocationNotifier sut = new(
            multiplexer.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await Should.NotThrowAsync(async () => await sut.PublishRevokedAsync("sess-1", ct));
    }
}
