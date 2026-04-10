using Microsoft.AspNetCore.Authentication;

namespace Identity.Security.Sessions;

/// <summary>
///     High-level application service for creating, loading, and updating persisted BFF sessions.
/// </summary>
public interface IBffSessionService
{
    /// <summary>
    ///     Creates a new server-side session record from the issued authentication ticket and
    ///     returns the opaque session id stored in the cookie.
    /// </summary>
    Task<string> CreateSessionAsync(AuthenticationTicket ticket, CancellationToken ct = default);

    /// <summary>
    ///     Loads the session and reconstructs the cookie authentication ticket when the session is
    ///     still valid.
    /// </summary>
    Task<AuthenticationTicket?> GetTicketAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    ///     Loads the current session snapshot when the session is still valid.
    /// </summary>
    Task<BffSessionRecord?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    ///     Updates the persisted session from the latest cookie authentication ticket contents.
    /// </summary>
    Task UpdateSessionFromTicketAsync(
        string sessionId,
        AuthenticationTicket ticket,
        CancellationToken ct = default
    );
}
