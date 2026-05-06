namespace Identity.Auth.Features.Bff.DTOs;

/// <summary>
///     Request model for direct LDAP authentication.
/// </summary>
/// <param name="Username">The LDAP username or Distinguished Name.</param>
/// <param name="Password">The LDAP password.</param>
public sealed record LdapLoginRequest(string Username, string Password);
