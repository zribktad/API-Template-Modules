using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Security.Sessions;

/// <summary>
///     Reconstructs cookie authentication objects from the persisted BFF session model.
/// </summary>
public interface IBffSessionPrincipalFactory
{
    /// <summary>
    ///     Builds a claims principal that mirrors the identity information stored in the session.
    /// </summary>
    ClaimsPrincipal CreatePrincipal(BffSessionRecord session);

    /// <summary>
    ///     Builds a full authentication ticket for the given scheme from the stored session.
    /// </summary>
    AuthenticationTicket CreateTicket(BffSessionRecord session, string authenticationScheme);
}
