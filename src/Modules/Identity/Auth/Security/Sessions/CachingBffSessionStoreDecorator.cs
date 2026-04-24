using System.Collections.Concurrent;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Decorates <see cref="IBffSessionStore" /> with an in-process skip-window cache and broadcasts
///     invalidation events so peer instances can evict their own local caches. Mutations go to the
///     inner store first; the local cache is updated/invalidated only after the store confirms the
///     change. Every successful mutation publishes to the revocation channel so peers that hold a
///     stale copy of the session drop it and re-fetch on the next request. Revocation broadcasts
///     run with <see cref="CancellationToken.None" /> so a client disconnect after the inner store
///     mutation succeeded cannot abort peer notification.
/// </summary>
public sealed class CachingBffSessionStoreDecorator : IBffSessionStore
{
    private readonly IBffSessionStore _inner;
    private readonly IBffLocalSessionCache _localCache;
    private readonly IBffSessionRevocationNotifier _notifier;
    private readonly ConcurrentDictionary<string, Lazy<Task<BffSessionRecord?>>> _inflight = new();

    public CachingBffSessionStoreDecorator(
        IBffSessionStore inner,
        IBffLocalSessionCache localCache,
        IBffSessionRevocationNotifier notifier
    )
    {
        _inner = inner;
        _localCache = localCache;
        _notifier = notifier;
    }

    public Task<BffSessionRecord?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        if (_localCache.TryGet(sessionId, out BffSessionRecord? cached))
            return Task.FromResult<BffSessionRecord?>(cached);

        // Coalesce concurrent misses for the same id into one inner fetch (cache-stampede guard).
        // The shared task is detached from any single caller's CancellationToken; each caller
        // observes the result through WaitAsync with their own token.
        Lazy<Task<BffSessionRecord?>> lazy = _inflight.GetOrAdd(
            sessionId,
            id => new Lazy<Task<BffSessionRecord?>>(() => FetchAndPopulateAsync(id))
        );

        return lazy.Value.WaitAsync(ct);
    }

    private async Task<BffSessionRecord?> FetchAndPopulateAsync(string sessionId)
    {
        try
        {
            // Snapshot the cache generation before the inner read. If an Invalidate races with the
            // fetch (including a pub/sub revocation arriving mid-flight), the generation advances
            // and we skip writing back so a freshly-invalidated entry is not repopulated stale.
            long generation = _localCache.Generation;
            BffSessionRecord? record = await _inner.GetAsync(sessionId, CancellationToken.None);
            if (record is not null && _localCache.Generation == generation)
                _localCache.Set(sessionId, record);

            return record;
        }
        finally
        {
            _inflight.TryRemove(sessionId, out _);
        }
    }

    public async Task StoreAsync(BffSessionRecord session, CancellationToken ct = default)
    {
        await _inner.StoreAsync(session, ct);

        if (IsTerminal(session.Status))
        {
            _localCache.Invalidate(session.SessionId);
            await _notifier.PublishRevokedAsync(session.SessionId, CancellationToken.None);
        }
        else
        {
            _localCache.Set(session.SessionId, session);
        }
    }

    public async Task<bool> TryUpdateAsync(
        BffSessionRecord session,
        long expectedVersion,
        CancellationToken ct = default
    )
    {
        bool updated = await _inner.TryUpdateAsync(session, expectedVersion, ct);
        if (!updated)
            return false;

        if (IsTerminal(session.Status))
            _localCache.Invalidate(session.SessionId);
        else
            _localCache.Set(session.SessionId, session);

        // Broadcast on every successful update — peers evict their L1 copies and re-fetch on next
        // access, bounding staleness to one round-trip instead of the full local-cache TTL.
        await _notifier.PublishRevokedAsync(session.SessionId, CancellationToken.None);
        return true;
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        await _inner.RemoveAsync(sessionId, ct);
        _localCache.Invalidate(sessionId);
        await _notifier.PublishRevokedAsync(sessionId, CancellationToken.None);
    }

    public Task<IReadOnlyList<string>> FindActiveSessionIdsBySubjectAsync(
        string keycloakSubject,
        CancellationToken ct = default
    ) => _inner.FindActiveSessionIdsBySubjectAsync(keycloakSubject, ct);

    public async Task<IReadOnlyList<string>> BulkRevokeActiveSessionsBySubjectAsync(
        string keycloakSubject,
        BffSessionRevocationReason reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken ct = default
    )
    {
        IReadOnlyList<string> revokedIds = await _inner.BulkRevokeActiveSessionsBySubjectAsync(
            keycloakSubject,
            reason,
            revokedAtUtc,
            ct
        );

        foreach (string id in revokedIds)
            _localCache.Invalidate(id);

        await Task.WhenAll(
            revokedIds.Select(id => _notifier.PublishRevokedAsync(id, CancellationToken.None))
        );

        return revokedIds;
    }

    private static bool IsTerminal(BffSessionStatus status) =>
        status is BffSessionStatus.Revoked or BffSessionStatus.Expired;
}
