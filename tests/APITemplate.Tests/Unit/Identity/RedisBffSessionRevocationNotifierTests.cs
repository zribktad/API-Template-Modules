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
public sealed class RedisBffSessionRevocationNotifierTests
{
    private static readonly RedisChannel Channel = BffSessionCacheKeys.RevocationChannel;

    [Fact]
    public async Task PublishRevokedAsync_WhenConnected_PublishesToBroadcastChannel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(true)
            .Build();

        RedisBffSessionRevocationNotifier sut = new(
            redis.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await sut.PublishRevokedAsync("sess-1", ct);

        redis.Subscriber.Verify(
            s => s.PublishAsync(Channel, "sess-1", It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishRevokedAsync_WhenConnectionStateIsFalse_StillAttemptsPublish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(false)
            .Build();

        RedisBffSessionRevocationNotifier sut = new(
            redis.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await sut.PublishRevokedAsync("sess-1", ct);

        redis.Subscriber.Verify(
            s => s.PublishAsync(Channel, "sess-1", It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishRevokedAsync_SwallowsRedisException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(true)
            .WithPublishThrowing<RedisConnectionException>()
            .Build();

        RedisBffSessionRevocationNotifier sut = new(
            redis.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await Should.NotThrowAsync(async () => await sut.PublishRevokedAsync("sess-1", ct));
    }

    [Fact]
    public async Task PublishRevokedAsync_SwallowsObjectDisposedException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(true)
            .WithPublishThrowing<ObjectDisposedException>()
            .Build();

        RedisBffSessionRevocationNotifier sut = new(
            redis.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await Should.NotThrowAsync(async () => await sut.PublishRevokedAsync("sess-1", ct));
    }

    [Fact]
    public async Task PublishRevokedAsync_PropagatesOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        TaskCompletionSource<long> publishGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(true)
            .WithPublishTask(publishGate.Task)
            .Build();

        RedisBffSessionRevocationNotifier sut = new(
            redis.Object,
            NullLogger<RedisBffSessionRevocationNotifier>.Instance
        );

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await sut.PublishRevokedAsync("sess-1", cts.Token)
        );
    }

    [Fact]
    public void SafeRef_WhenSessionIdIsEmpty_ReturnsEmptyPlaceholder()
    {
        RedisBffSessionRevocationNotifier.SafeRef("").ShouldBe("(empty)");
    }

    [Fact]
    public void SafeRef_WhenSessionIdIsShorterThanEightChars_ReturnsPrefixWithEllipsis()
    {
        RedisBffSessionRevocationNotifier.SafeRef("abc").ShouldBe("abc...");
    }

    [Fact]
    public void SafeRef_WhenSessionIdIsExactlyEightChars_ReturnsFullValueWithEllipsis()
    {
        RedisBffSessionRevocationNotifier.SafeRef("12345678").ShouldBe("12345678...");
    }

    [Fact]
    public void SafeRef_WhenSessionIdIsLongerThanEightChars_ReturnsTruncatedPrefixWithEllipsis()
    {
        string sessionId = "1234567890abcdef1234567890abcdef";
        RedisBffSessionRevocationNotifier.SafeRef(sessionId).ShouldBe("12345678...");
    }

    [Fact]
    public void SafeRef_NeverContainsFullSessionIdForLongIds()
    {
        string sessionId = "1234567890abcdef1234567890abcdef";
        RedisBffSessionRevocationNotifier.SafeRef(sessionId).ShouldNotContain(sessionId);
    }

    [Fact]
    public async Task PublishRevokedAsync_LogsRedactedSessionRef_NotFullId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string sessionId = "1234567890abcdef1234567890abcdef";
        string? capturedMessage = null;
        Mock<ILogger<RedisBffSessionRevocationNotifier>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger
            .Setup(l =>
                l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Callback<LogLevel, EventId, object, Exception?, Delegate>(
                (_, _, state, ex, formatter) =>
                    capturedMessage = formatter.DynamicInvoke(state, ex) as string
            );
        RedisConnectionMultiplexerMockHarness redis = new RedisConnectionMultiplexerMockBuilder()
            .WithConnected(true)
            .WithPublishThrowing<RedisConnectionException>()
            .Build();

        RedisBffSessionRevocationNotifier sut = new(redis.Object, logger.Object);

        await sut.PublishRevokedAsync(sessionId, ct);

        logger.Verify(
            x =>
                x.Log(
                    It.IsAny<LogLevel>(),
                    It.Is<EventId>(e => e.Id == 3075),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
        capturedMessage.ShouldNotBeNull();
        capturedMessage.ShouldContain("12345678...");
        capturedMessage.ShouldNotContain(sessionId);
    }
}
