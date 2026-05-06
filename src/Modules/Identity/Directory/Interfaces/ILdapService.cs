using ErrorOr;
using Identity.Directory.Features.User;

namespace Identity.Directory.Interfaces;

/// <summary>
///     Defines the contract for native LDAP operations.
/// </summary>
public interface ILdapService
{
    /// <summary>
    ///     Attempts to authenticate a user by performing a Simple Bind operation.
    /// </summary>
    /// <param name="username">The username (or DN) to authenticate.</param>
    /// <param name="password">The password to verify.</param>
    /// <returns>LDAP user details if authenticated; otherwise an error.</returns>
    Task<ErrorOr.ErrorOr<LdapUserResponse>> AuthenticateAsync(
        string username,
        string password,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Searches for a user in the LDAP directory.
    /// </summary>
    /// <param name="username">The username to search for.</param>
    /// <returns>LDAP user details if found; otherwise null.</returns>
    Task<LdapUserResponse?> GetUserAsync(string username, CancellationToken ct = default);
}
