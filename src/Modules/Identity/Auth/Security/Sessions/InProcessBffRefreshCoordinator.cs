using Identity.Auth.Options;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Refresh coordinator for single-instance deployments without Redis: serializes concurrent
///     refresh attempts per session in memory only.
/// </summary>
public sealed class InProcessBffRefreshCoordinator : IBffRefreshCoordinator
{
    private readonly InProcessBffRefreshCore _core;

    public InProcessBffRefreshCoordinator(IOptions<BffOptions> options)
    {
        _core = new InProcessBffRefreshCore(options.Value);
    }

    /// <inheritdoc />
    public Task<BffRefreshOutcome> ExecuteAsync(
        string sessionId,
        Func<CancellationToken, Task<BffRefreshOutcome>> leaderAction,
        Func<CancellationToken, Task<BffRefreshOutcome>> followerAction,
        CancellationToken ct = default
    ) => _core.ExecuteAsync(sessionId, leaderAction, followerAction, ct);
}
