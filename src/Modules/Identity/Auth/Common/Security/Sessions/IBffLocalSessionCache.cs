using System.Diagnostics.CodeAnalysis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     In-process skip-window cache over <see cref="IBffSessionStore" /> that lets repeated requests
///     for the same session reuse the last fetched record without a distributed lookup.
/// </summary>
public interface IBffLocalSessionCache
{
    /// <summary>
    ///     Monotonically increasing counter bumped on every <see cref="Invalidate" /> call. Callers
    ///     performing a read-through fetch can snapshot this value before hitting the inner store and
    ///     skip writing the result back when it has changed — preventing a concurrent invalidation
    ///     from being overwritten by an in-flight stale record.
    /// </summary>
    long Generation { get; }

    /// <summary>
    ///     Attempts to read a cached session record. Returns <see langword="false" /> when the entry
    ///     is missing, expired, or when the cache is disabled.
    /// </summary>
    bool TryGet(string sessionId, [NotNullWhen(true)] out BffSessionRecord? record);

    /// <summary>Stores or replaces the cached record for <paramref name="sessionId" />.</summary>
    void Set(string sessionId, BffSessionRecord record);

    /// <summary>Evicts the cached entry, if any, for <paramref name="sessionId" />.</summary>
    void Invalidate(string sessionId);
}
