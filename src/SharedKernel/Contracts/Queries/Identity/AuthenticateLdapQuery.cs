namespace SharedKernel.Contracts.Queries.Identity;

/// <summary>
///     Query to authenticate a user against LDAP and retrieve their basic info.
/// </summary>
public sealed record AuthenticateLdapQuery(string Username, string Password);

/// <summary>
///     Basic LDAP user information returned to other modules.
/// </summary>
public sealed record LdapUserContract(
    Guid LocalId,
    string Username,
    string? Email,
    string? DisplayName,
    string? DistinguishedName
);
