namespace Identity.Auth.Security.Sessions;

/// <summary>
///     In-process skip-window cache over <see cref="IBffSessionStore" /> that lets repeated requests
///     for the same session reuse the last fetched record without a distributed lookup.
/// </summary>
public interface IBffLocalSessionCache
{
    /// <summary>
    ///     Attempts to read a cached session record. Returns <see langword="false" /> when the entry
    ///     is missing, expired, or when the cache is disabled.
    /// </summary>
    bool TryGet(string sessionId, out BffSessionRecord? record);

    /// <summary>Stores or replaces the cached record for <paramref name="sessionId" />.</summary>
    void Set(string sessionId, BffSessionRecord record);

    /// <summary>Evicts the cached entry, if any, for <paramref name="sessionId" />.</summary>
    void Invalidate(string sessionId);

    /// <summary>Clears every cached entry.</summary>
    void Clear();
}
