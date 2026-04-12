namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Persistence abstraction for server-side BFF session records.
/// </summary>
public interface IBffSessionStore
{
    /// <summary>
    ///     Retrieves the session for the specified identifier, or <see langword="null" /> when it
    ///     does not exist.
    /// </summary>
    Task<BffSessionRecord?> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    ///     Stores a session snapshot.
    /// </summary>
    Task StoreAsync(BffSessionRecord session, CancellationToken ct = default);

    /// <summary>
    ///     Attempts to update a session only when the expected optimistic concurrency version
    ///     matches the current stored version.
    /// </summary>
    Task<bool> TryUpdateAsync(
        BffSessionRecord session,
        long expectedVersion,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the session from the backing store.
    /// </summary>
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
}
