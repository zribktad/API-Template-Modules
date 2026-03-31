namespace SharedKernel.Application.Contracts;

/// <summary>
/// Application-layer abstraction for the idempotency store used to short-circuit duplicate
/// requests and replay cached responses without re-executing business logic.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Retrieves a previously cached response entry for <paramref name="key"/>,
    /// or returns <c>null</c> if no entry exists.
    /// </summary>
    Task<IdempotencyCacheEntry?> TryGetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Atomically checks if the key exists and acquires a lock if not.
    /// Returns true if the lock was acquired (key was not present), false otherwise.
    /// </summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Stores <paramref name="entry"/> under <paramref name="key"/> with the given <paramref name="ttl"/>,
    /// replacing the in-flight lock entry so subsequent duplicates receive the cached response.
    /// </summary>
    Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    );

    /// <summary>
    /// Releases the lock for the given key so a retry with the same key can proceed.
    /// Only releases if the lock is still owned (not yet replaced by a cached result).
    /// </summary>
    Task ReleaseAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Cached HTTP response snapshot stored by the idempotency middleware for replay on duplicate requests.
/// </summary>
public sealed record IdempotencyCacheEntry(
    int StatusCode,
    string? ResponseBody,
    string? ResponseContentType,
    string? LocationHeader = null
);
