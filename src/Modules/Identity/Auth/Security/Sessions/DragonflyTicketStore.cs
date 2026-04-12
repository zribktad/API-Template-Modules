using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Cookie session store adapter that persists the opaque cookie key as the BFF session id and
///     delegates all session state to the explicit server-side BFF session subsystem.
/// </summary>
public sealed class DragonflyTicketStore : ITicketStore
{
    private readonly IBffSessionService _sessionService;
    private readonly IBffSessionRevocationService _sessionRevocationService;

    public DragonflyTicketStore(
        IBffSessionService sessionService,
        IBffSessionRevocationService sessionRevocationService
    )
    {
        _sessionService = sessionService;
        _sessionRevocationService = sessionRevocationService;
    }

    /// <summary>
    ///     Creates a new server-side BFF session and returns its opaque identifier for cookie
    ///     storage.
    /// </summary>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        return await _sessionService.CreateSessionAsync(ticket);
    }

    /// <summary>
    ///     Synchronizes the persisted server-side session with the latest cookie ticket state.
    /// </summary>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await _sessionService.UpdateSessionFromTicketAsync(key, ticket);
    }

    /// <summary>
    ///     Loads the server-side session and reconstructs the cookie authentication ticket.
    ///     Stores the session identifier in <see cref="AuthenticationProperties.Items"/> so
    ///     downstream event handlers can resolve it without reading the raw cookie.
    /// </summary>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        AuthenticationTicket? ticket = await _sessionService.GetTicketAsync(key);
        if (ticket is not null)
            ticket.Properties.Items[AuthConstants.SessionProperties.SessionId] = key;

        return ticket;
    }

    /// <summary>
    ///     Revokes the server-side session associated with the cookie key during sign-out.
    /// </summary>
    public Task RemoveAsync(string key)
    {
        return _sessionRevocationService.RevokeAsync(key, BffSessionRevocationReason.Logout);
    }
}
