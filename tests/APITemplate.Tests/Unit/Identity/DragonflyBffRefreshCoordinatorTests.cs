using System.Text.Json;
using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class DragonflyBffRefreshCoordinatorTests
{
    private const string SessionId = "test-session-42";

    private static readonly string ResultKey = $"bff:session:{SessionId}:refresh:result";

    private static readonly RedisChannel NotifyChannel = RedisChannel.Literal(
        $"bff:session:{SessionId}:refresh:notify"
    );

    private static readonly BffSessionRecord StubSession = new()
    {
        SessionId = SessionId,
        UserId = "user-1",
    };

    private static readonly BffRefreshOutcome SuccessOutcome = BffRefreshOutcome.Success(
        StubSession
    );

    private static readonly BffRefreshOutcome FailedOutcome = BffRefreshOutcome.Failed(
        BffSessionRevocationReason.RefreshRejected
    );

    [Fact]
    public async Task Leader_PublishesNotificationAfterWritingOutcome()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<IDatabase> database = CreateDatabaseMock(lockAcquired: true);
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = CreateMultiplexerMock(
            database.Object,
            subscriber.Object
        );

        DragonflyBffRefreshCoordinator sut = CreateSut(multiplexer.Object);

        await sut.ExecuteAsync(
            SessionId,
            leaderAction: _ => Task.FromResult(SuccessOutcome),
            followerAction: _ => Task.FromResult(SuccessOutcome),
            ct
        );

        subscriber.Verify(
            s => s.PublishAsync(NotifyChannel, "done", It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Follower_WakesOnNotificationAndInvokesFollowerAction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<IDatabase> database = CreateDatabaseMock(lockAcquired: false);
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = CreateMultiplexerMock(
            database.Object,
            subscriber.Object
        );

        Action<RedisChannel, RedisValue>? capturedHandler = null;
        subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    NotifyChannel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler
            );

        // First StringGetAsync (after subscribe) returns empty — leader hasn't finished yet.
        // Second StringGetAsync (after notification) returns the outcome.
        string outcomeJson = SerializeOutcome(succeeded: true);
        SetupSequentialGets(database, RedisValue.Null, outcomeJson);

        bool followerInvoked = false;
        Task<BffRefreshOutcome> executeTask = sut_ExecuteAsFollowerAsync(
            sut: CreateSut(multiplexer.Object),
            followerAction: _ =>
            {
                followerInvoked = true;
                return Task.FromResult(SuccessOutcome);
            },
            ct
        );

        // Simulate leader completing — fire the pub/sub callback.
        capturedHandler.ShouldNotBeNull();
        capturedHandler(NotifyChannel, "done");

        BffRefreshOutcome result = await executeTask;

        followerInvoked.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();
        subscriber.Verify(
            s => s.UnsubscribeAsync(NotifyChannel, null, It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Follower_FindsResultOnInitialGet_ReturnsImmediately()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<IDatabase> database = CreateDatabaseMock(lockAcquired: false);
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = CreateMultiplexerMock(
            database.Object,
            subscriber.Object
        );

        // The very first GET after subscribe already has the result (race: leader finished fast).
        string outcomeJson = SerializeOutcome(succeeded: true);
        database
            .Setup(d => d.StringGetAsync(ResultKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)outcomeJson);

        bool followerInvoked = false;
        BffRefreshOutcome result = await sut_ExecuteAsFollowerAsync(
            sut: CreateSut(multiplexer.Object),
            followerAction: _ =>
            {
                followerInvoked = true;
                return Task.FromResult(SuccessOutcome);
            },
            ct
        );

        followerInvoked.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();
        subscriber.Verify(
            s => s.UnsubscribeAsync(NotifyChannel, null, It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Follower_TimesOut_ReturnsFailedAndUnsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<IDatabase> database = CreateDatabaseMock(lockAcquired: false);
        Mock<ISubscriber> subscriber = new();
        Mock<IConnectionMultiplexer> multiplexer = CreateMultiplexerMock(
            database.Object,
            subscriber.Object
        );

        // All GETs return empty — leader never finishes.
        database
            .Setup(d => d.StringGetAsync(ResultKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Never fire the subscription handler → forces timeout.
        BffRefreshOutcome result = await sut_ExecuteAsFollowerAsync(
            sut: CreateSut(multiplexer.Object, refreshWaitTimeoutMs: 200),
            followerAction: _ => Task.FromResult(SuccessOutcome),
            ct
        );

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldBe(BffSessionRevocationReason.RefreshRejected);
        subscriber.Verify(
            s => s.UnsubscribeAsync(NotifyChannel, null, It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task FallbackSemaphore_WhenDisconnected_UsesInProcessCoordination()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.SetupGet(x => x.IsConnected).Returns(false);

        DragonflyBffRefreshCoordinator sut = CreateSut(multiplexer.Object);
        bool leaderInvoked = false;

        BffRefreshOutcome result = await sut.ExecuteAsync(
            SessionId,
            leaderAction: _ =>
            {
                leaderInvoked = true;
                return Task.FromResult(SuccessOutcome);
            },
            followerAction: _ => Task.FromResult(SuccessOutcome),
            ct
        );

        leaderInvoked.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();

        // Verify no Redis database interaction occurred.
        multiplexer.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DragonflyBffRefreshCoordinator CreateSut(
        IConnectionMultiplexer multiplexer,
        int refreshWaitTimeoutMs = 2000
    ) =>
        new(
            multiplexer,
            Options.Create(new BffOptions { RefreshWaitTimeoutMilliseconds = refreshWaitTimeoutMs })
        );

    private static Mock<IDatabase> CreateDatabaseMock(bool lockAcquired)
    {
        Mock<IDatabase> database = new();
        database
            .Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                )
            )
            .ReturnsAsync(lockAcquired);

        if (lockAcquired)
        {
            database
                .Setup(d =>
                    d.ScriptEvaluateAsync(
                        It.IsAny<LuaScript>(),
                        It.IsAny<object>(),
                        It.IsAny<CommandFlags>()
                    )
                )
                .ReturnsAsync(RedisResult.Create(1));
        }

        return database;
    }

    private static Mock<IConnectionMultiplexer> CreateMultiplexerMock(
        IDatabase database,
        ISubscriber subscriber
    )
    {
        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.SetupGet(x => x.IsConnected).Returns(true);
        multiplexer
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database);
        multiplexer.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(subscriber);
        return multiplexer;
    }

    private static Task<BffRefreshOutcome> sut_ExecuteAsFollowerAsync(
        DragonflyBffRefreshCoordinator sut,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct
    ) =>
        sut.ExecuteAsync(
            SessionId,
            leaderAction: _ => throw new InvalidOperationException("Leader should not be called"),
            followerAction,
            ct
        );

    private static string SerializeOutcome(
        bool succeeded,
        BffSessionRevocationReason? failureReason = null
    )
    {
        var payload = new { Succeeded = succeeded, FailureReason = failureReason };
        return JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
    }

    private static void SetupSequentialGets(Mock<IDatabase> database, params RedisValue[] values)
    {
        MockSequence sequence = new();
        foreach (RedisValue value in values)
        {
            database
                .InSequence(sequence)
                .Setup(d => d.StringGetAsync(ResultKey, It.IsAny<CommandFlags>()))
                .ReturnsAsync(value);
        }
    }
}
