using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Helpers for reading BFF session values from <see cref="AuthenticationProperties" />.
/// </summary>
public static class BffAuthenticationPropertiesExtensions
{
    /// <summary>
    ///     Resolves the opaque BFF session id stored on the cookie authentication ticket.
    /// </summary>
    public static bool TryGetBffSessionId(
        this AuthenticationProperties? properties,
        [NotNullWhen(true)] out string? sessionId
    )
    {
        sessionId = null;
        if (
            properties?.Items.TryGetValue(
                AuthConstants.SessionProperties.SessionId,
                out string? sid
            ) != true
            || string.IsNullOrWhiteSpace(sid)
        )
            return false;

        sessionId = sid;
        return true;
    }
}
