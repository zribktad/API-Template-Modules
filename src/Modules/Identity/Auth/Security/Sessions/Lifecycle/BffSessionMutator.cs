using Identity.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Auth.Security.Sessions.Lifecycle;

internal sealed class BffSessionMutator : IBffSessionMutator
{
    private const int MaxAttempts = 5;

    private readonly IBffSessionStore _sessionStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BffSessionMutator> _logger;

    public BffSessionMutator(
        IBffSessionStore sessionStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BffSessionMutator> logger
    )
    {
        _sessionStore = sessionStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task MutateAsync(
        string sessionId,
        Func<BffSessionRecord, BffSessionRecord> mutate,
        CancellationToken ct = default
    )
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            BffSessionRecord? currentSession = await _sessionStore.GetAsync(sessionId, ct);
            if (currentSession is null)
                return;

            BffSessionRecord updatedSession = mutate(currentSession);
            bool updated = await _sessionStore.TryUpdateAsync(
                updatedSession,
                currentSession.Version,
                ct
            );
            if (updated)
            {
                BffRequestScopedSessionCache.Invalidate(_httpContextAccessor, sessionId);
                return;
            }
        }

        _logger.BffSessionMutationFailed(sessionId, MaxAttempts);
    }
}
