using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class InProcessBffRefreshCoordinatorTests
{
    private static readonly BffSessionRecord StubSession = new()
    {
        SessionId = "session",
        UserId = "user",
    };

    [Fact]
    public async Task ExecuteAsync_SingleLeader_ReturnsLeaderOutcomeWithoutFollower()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(
            new BffOptions
            {
                RefreshLockTimeoutMilliseconds = 9_000,
                RefreshWaitTimeoutMilliseconds = 10_000,
            }
        );
        var coordinator = new InProcessBffRefreshCoordinator(options);

        BffRefreshOutcome outcome = await coordinator.ExecuteAsync(
            "session-single",
            _ => Task.FromResult(BffRefreshOutcome.Success(StubSession)),
            _ =>
                Task.FromResult(
                    BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected)
                ),
            ct
        );

        outcome.Succeeded.ShouldBeTrue();
        outcome.Session.ShouldBe(StubSession);
    }

    [Fact]
    public async Task ExecuteAsync_SecondCallerOnSameSession_UsesFollowerAfterLeaderCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(
            new BffOptions
            {
                RefreshLockTimeoutMilliseconds = 9_000,
                RefreshWaitTimeoutMilliseconds = 10_000,
            }
        );
        var coordinator = new InProcessBffRefreshCoordinator(options);
        const string sessionId = "session-dup";

        Task<BffRefreshOutcome> first = coordinator.ExecuteAsync(
            sessionId,
            async _ =>
            {
                await Task.Delay(30, ct);
                return BffRefreshOutcome.Success(StubSession);
            },
            _ => Task.FromResult(BffRefreshOutcome.NotRequired(StubSession)),
            ct
        );

        await Task.Delay(5, ct);

        Task<BffRefreshOutcome> second = coordinator.ExecuteAsync(
            sessionId,
            _ => Task.FromResult(BffRefreshOutcome.Success(StubSession)),
            _ => Task.FromResult(BffRefreshOutcome.NotRequired(StubSession)),
            ct
        );

        (await first).Succeeded.ShouldBeTrue();
        (await second).Succeeded.ShouldBeTrue();
        (await second).RequiresRenewal.ShouldBeFalse();
    }
}
