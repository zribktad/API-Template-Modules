using APITemplate.Tests.Unit.Identity.Mocks;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionRevocationSubscriberTests
{
    private static readonly RedisChannel Channel = BffSessionCacheKeys.RevocationChannel;

    [Fact]
    public async Task OnStart_SubscribesToRevocationChannel()
    {
        RedisConnectionMultiplexerMockHarness redis =
            new RedisConnectionMultiplexerMockBuilder().Build();

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            redis.Subscriber.Verify(
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
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithCapturedSubscribeHandler(out Func<Action<RedisChannel, RedisValue>?> getHandler)
            .Build();

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            Action<RedisChannel, RedisValue>? capturedHandler = getHandler();
            capturedHandler.ShouldNotBeNull();
            capturedHandler(Channel, "00000000000000000000000000000042");

            cache.Verify(c => c.Invalidate("00000000000000000000000000000042"), Times.Once);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0000000000000000000000000000000")]
    [InlineData("000000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000A")]
    [InlineData("0000000000000000000000000000000z")]
    public async Task OnMessage_WithMalformedPayload_DoesNotInvalidate(string payload)
    {
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithCapturedSubscribeHandler(out Func<Action<RedisChannel, RedisValue>?> getHandler)
            .Build();

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            Action<RedisChannel, RedisValue>? capturedHandler = getHandler();
            capturedHandler.ShouldNotBeNull();
            capturedHandler(Channel, payload);

            cache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnMessage_WithNullOrBinaryPayload_DoesNotInvalidate()
    {
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithCapturedSubscribeHandler(out Func<Action<RedisChannel, RedisValue>?> getHandler)
            .Build();

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        try
        {
            Action<RedisChannel, RedisValue>? capturedHandler = getHandler();
            capturedHandler.ShouldNotBeNull();
            capturedHandler(Channel, RedisValue.Null);
            capturedHandler(Channel, new byte[] { 0, 1, 2, 3 });

            cache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnConnectionRestored_WhenAlreadySubscribed_DoesNotSubscribeAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis =
            new RedisConnectionMultiplexerMockBuilder().Build();
        Mock<IBffLocalSessionCache> cache = new();
        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(ct);
        try
        {
            redis.RaiseConnectionRestored();
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);

            int subscribeCalls = redis.Subscriber.Invocations.Count(invocation =>
                invocation.Method.Name == nameof(ISubscriber.SubscribeAsync)
            );

            subscribeCalls.ShouldBe(1);
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    [Fact]
    public async Task OnConnectionRestored_WhenInitialSubscribeFailed_Retries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithFirstSubscribeThrowing<RedisConnectionException>()
            .Build();
        Mock<IBffLocalSessionCache> cache = new();
        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(ct);
        try
        {
            redis.RaiseConnectionRestored();
            bool retried = await BffSessionStoreUnitTestHelpers.WaitUntilAsync(
                () =>
                    redis.Subscriber.Invocations.Count(invocation =>
                        invocation.Method.Name == nameof(ISubscriber.SubscribeAsync)
                    ) == 2,
                ct
            );

            retried.ShouldBeTrue();
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    [Fact]
    public async Task StartAsync_WhenInitialSubscribeThrows_DoesNotPropagate()
    {
        Mock<ILogger<BffSessionRevocationSubscriber>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithSubscribeThrowing<RedisConnectionException>()
            .Build();
        Mock<IBffLocalSessionCache> cache = new();
        BffSessionRevocationSubscriber sut = new(redis.Object, cache.Object, logger.Object);

        await Should.NotThrowAsync(async () => await sut.StartAsync(CancellationToken.None));

        VerifyLogged(logger, 3070);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OnConnectionRestored_WhenRetrySubscribeStillFails_Swallows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithSubscribeThrowing<RedisConnectionException>()
            .Build();
        Mock<IBffLocalSessionCache> cache = new();
        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(ct);

        Should.NotThrow(redis.RaiseConnectionRestored);
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        await sut.StopAsync(ct);
    }

    [Fact]
    public async Task HandleMessage_WhenInvalidateThrows_LogsAndContinues()
    {
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithCapturedSubscribeHandler(out Func<Action<RedisChannel, RedisValue>?> getHandler)
            .Build();
        Mock<IBffLocalSessionCache> cache = new();
        Mock<ILogger<BffSessionRevocationSubscriber>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        int invalidateCalls = 0;
        cache
            .Setup(c => c.Invalidate(It.IsAny<string>()))
            .Callback<string>(_ =>
            {
                if (Interlocked.Increment(ref invalidateCalls) == 1)
                    throw new InvalidOperationException("boom");
            });
        BffSessionRevocationSubscriber sut = new(redis.Object, cache.Object, logger.Object);

        await sut.StartAsync(CancellationToken.None);
        try
        {
            Action<RedisChannel, RedisValue>? capturedHandler = getHandler();
            capturedHandler.ShouldNotBeNull();
            capturedHandler(Channel, "00000000000000000000000000000042");
            capturedHandler(Channel, "00000000000000000000000000000043");

            VerifyLogged(logger, 3072);
            cache.Verify(c => c.Invalidate("00000000000000000000000000000043"), Times.Once);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_DetachesConnectionRestoredHandler()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis =
            new RedisConnectionMultiplexerMockBuilder().Build();
        Mock<IBffLocalSessionCache> cache = new();
        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(ct);
        await sut.StopAsync(ct);

        redis.RaiseConnectionRestored();
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);

        redis.Subscriber.Verify(
            s =>
                s.SubscribeAsync(
                    Channel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnStop_UnsubscribesFromChannel()
    {
        RedisConnectionMultiplexerMockHarness redis =
            new RedisConnectionMultiplexerMockBuilder().Build();

        Mock<IBffLocalSessionCache> cache = new();

        BffSessionRevocationSubscriber sut = new(
            redis.Object,
            cache.Object,
            NullLogger<BffSessionRevocationSubscriber>.Instance
        );

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        redis.Subscriber.Verify(
            s =>
                s.UnsubscribeAsync(
                    Channel,
                    It.IsAny<Action<RedisChannel, RedisValue>?>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    private static void VerifyLogged(
        Mock<ILogger<BffSessionRevocationSubscriber>> logger,
        int eventId
    )
    {
        logger.Verify(
            x =>
                x.Log(
                    It.IsAny<LogLevel>(),
                    It.Is<EventId>(e => e.Id == eventId),
                    It.Is<It.IsAnyType>((_, _) => true),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.AtLeastOnce
        );
    }
}
