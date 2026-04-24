using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionRevocationSubscriberTests
{
    private static readonly RedisChannel Channel = RedisChannel.Literal("bff:session:revocations");

    [Fact]
    public async Task OnStart_SubscribesToRevocationChannel()
    {
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            multiplexer.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            subscriber.Verify(
                s =>
                    s.SubscribeAsync(
                        Channel,
                        It.IsAny<Action<RedisChannel, RedisValue>>(),
                        It.IsAny<CommandFlags>()
                    ),
                Times.Once
            );
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnMessage_InvalidatesLocalCache()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        Mock<ISubscriber> subscriber = new();
        subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    Channel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler
            )
            .Returns(Task.CompletedTask);

        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            multiplexer.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            capturedHandler.ShouldNotBeNull();
            capturedHandler!(Channel, "sess-42");

            cache.Verify(c => c.Invalidate("sess-42"), Times.Once);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnMessage_WithEmptyPayload_DoesNotInvalidate()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        Mock<ISubscriber> subscriber = new();
        subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    Channel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler
            )
            .Returns(Task.CompletedTask);

        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            multiplexer.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            capturedHandler!(Channel, RedisValue.Null);
            capturedHandler!(Channel, "");

            cache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnStop_UnsubscribesFromChannel()
    {
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            multiplexer.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        subscriber.Verify(
            s =>
                s.UnsubscribeAsync(
                    Channel,
                    It.IsAny<Action<RedisChannel, RedisValue>?>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }
}
