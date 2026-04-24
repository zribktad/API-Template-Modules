namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Decorates <see cref="IBffSessionStore" /> with an in-process skip-window cache and broadcasts
///     revocation events so peer instances can invalidate their own local caches. Mutations go to
///     the inner store first; the local cache is updated/invalidated only after the store confirms
///     the change.
/// </summary>
public sealed class CachingBffSessionStoreDecorator : IBffSessionStore
{
    private readonly IBffSessionStore _inner;
    private readonly IBffLocalSessionCache _localCache;
    private readonly IBffSessionRevocationNotifier _notifier;

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

    public async Task<BffSessionRecord?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        if (_localCache.TryGet(sessionId, out BffSessionRecord? cached))
            return cached;

        BffSessionRecord? record = await _inner.GetAsync(sessionId, ct);
        if (record is not null)
            _localCache.Set(sessionId, record);

        return record;
    }

    public async Task StoreAsync(BffSessionRecord session, CancellationToken ct = default)
    {
        await _inner.StoreAsync(session, ct);
        _localCache.Set(session.SessionId, session);
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
        {
            _localCache.Invalidate(session.SessionId);
            await _notifier.PublishRevokedAsync(session.SessionId, ct);
        }
        else
        {
            _localCache.Set(session.SessionId, session);
        }

        return true;
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        await _inner.RemoveAsync(sessionId, ct);
        _localCache.Invalidate(sessionId);
        await _notifier.PublishRevokedAsync(sessionId, ct);
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

        foreach (string sessionId in revokedIds)
        {
            _localCache.Invalidate(sessionId);
            await _notifier.PublishRevokedAsync(sessionId, ct);
        }

        return revokedIds;
    }

    private static bool IsTerminal(BffSessionStatus status) =>
        status is BffSessionStatus.Revoked or BffSessionStatus.Expired;
}
