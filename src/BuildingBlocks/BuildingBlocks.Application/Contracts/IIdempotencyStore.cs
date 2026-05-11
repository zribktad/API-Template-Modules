namespace BuildingBlocks.Application.Contracts;

/// <summary>
///     Application-layer abstraction for the idempotency store used to short-circuit duplicate
///     requests and replay cached responses without re-executing business logic.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    ///     Retrieves a previously cached response entry for <paramref name="key" />,
    ///     or returns <c>null</c> if no entry exists.
    /// </summary>
    Task<IdempotencyCacheEntry?> TryGetAsync(string key, CancellationToken ct = default);

    Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    );

    Task ReleaseAsync(string key, string lockToken, CancellationToken ct = default);
}

/// <summary>
///     Cached HTTP response snapshot stored by the idempotency middleware for replay on duplicate requests.
/// </summary>
public sealed record IdempotencyCacheEntry(
    int StatusCode,
    string? ResponseBody,
    string? ResponseContentType,
    string? LocationHeader = null
);
