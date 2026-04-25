namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Publishes BFF session revocation events so peer instances can evict their local session
///     caches before the TTL lapses.
/// </summary>
public interface IBffSessionRevocationNotifier
{
    /// <summary>
    ///     Broadcasts that the session identified by <paramref name="sessionId" /> has been revoked,
    ///     expired, or otherwise mutated into a terminal state.
    /// </summary>
    Task PublishRevokedAsync(string sessionId, CancellationToken ct = default);
}
