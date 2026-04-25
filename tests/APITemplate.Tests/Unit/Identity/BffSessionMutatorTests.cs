using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionMutatorTests
{
    private const int MaxAttempts = 5;

    private readonly Mock<IBffSessionStore> _sessionStore = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    private BffSessionMutator CreateSut(ILogger<BffSessionMutator>? logger = null) =>
        new(
            _sessionStore.Object,
            _httpContextAccessor.Object,
            logger ?? NullLogger<BffSessionMutator>.Instance
        );

    [Fact]
    public async Task MutateAsync_WhenSessionNotFound_DoesNotCallUpdate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string sessionId = "session-not-found";

        _sessionStore.Setup(s => s.GetAsync(sessionId, ct)).ReturnsAsync((BffSessionRecord?)null);

        BffSessionMutator sut = CreateSut();

        await sut.MutateAsync(sessionId, s => s, ct);

        _sessionStore.Verify(
            s =>
                s.TryUpdateAsync(
                    It.IsAny<BffSessionRecord>(),
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task MutateAsync_WhenFirstAttemptSucceeds_CallsUpdateOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();

        _sessionStore.Setup(s => s.GetAsync(session.SessionId, ct)).ReturnsAsync(session);
        _sessionStore
            .Setup(s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct))
            .ReturnsAsync(true);

        BffSessionMutator sut = CreateSut();

        await sut.MutateAsync(session.SessionId, s => s, ct);

        _sessionStore.Verify(s => s.GetAsync(session.SessionId, ct), Times.Once);
        _sessionStore.Verify(
            s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct),
            Times.Once
        );
    }

    [Fact]
    public async Task MutateAsync_WhenVersionConflict_RetriesUntilSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        int tryUpdateCallCount = 0;

        _sessionStore.Setup(s => s.GetAsync(session.SessionId, ct)).ReturnsAsync(session);
        _sessionStore
            .Setup(s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct))
            .ReturnsAsync(() =>
            {
                tryUpdateCallCount++;
                return tryUpdateCallCount >= 3;
            });

        BffSessionMutator sut = CreateSut();

        await sut.MutateAsync(session.SessionId, s => s, ct);

        _sessionStore.Verify(s => s.GetAsync(session.SessionId, ct), Times.Exactly(3));
        _sessionStore.Verify(
            s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct),
            Times.Exactly(3)
        );
    }

    [Fact]
    public async Task MutateAsync_WhenAllAttemptsExhausted_LogsAndDoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        Mock<ILogger<BffSessionMutator>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _sessionStore.Setup(s => s.GetAsync(session.SessionId, ct)).ReturnsAsync(session);
        _sessionStore
            .Setup(s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct))
            .ReturnsAsync(false);

        BffSessionMutator sut = CreateSut(logger.Object);

        await Should.NotThrowAsync(async () =>
            await sut.MutateAsync(session.SessionId, s => s, ct)
        );

        logger.Verify(
            x =>
                x.Log(
                    It.IsAny<LogLevel>(),
                    It.Is<EventId>(e => e.Id == 3050),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task MutateAsync_WhenAllAttemptsExhausted_GetAsyncCalledExactlyFiveTimes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();

        _sessionStore.Setup(s => s.GetAsync(session.SessionId, ct)).ReturnsAsync(session);
        _sessionStore
            .Setup(s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct))
            .ReturnsAsync(false);

        BffSessionMutator sut = CreateSut();

        await sut.MutateAsync(session.SessionId, s => s, ct);

        _sessionStore.Verify(s => s.GetAsync(session.SessionId, ct), Times.Exactly(MaxAttempts));
    }

    [Fact]
    public async Task MutateAsync_MutationFunctionAppliedToCurrentSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        BffSessionRecord? capturedInput = null;

        _sessionStore.Setup(s => s.GetAsync(session.SessionId, ct)).ReturnsAsync(session);
        _sessionStore
            .Setup(s => s.TryUpdateAsync(It.IsAny<BffSessionRecord>(), session.Version, ct))
            .ReturnsAsync(true);

        BffSessionMutator sut = CreateSut();

        await sut.MutateAsync(
            session.SessionId,
            s =>
            {
                capturedInput = s;
                return s;
            },
            ct
        );

        capturedInput.ShouldNotBeNull();
        capturedInput.ShouldBe(session);
    }
}
