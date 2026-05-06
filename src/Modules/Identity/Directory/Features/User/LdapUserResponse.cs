namespace Identity.Directory.Features.User;

/// <summary>
///     Represents user data retrieved from an LDAP directory.
/// </summary>
public sealed record LdapUserResponse(
    string Username,
    string? Email,
    string? DisplayName,
    string? DistinguishedName,
    IReadOnlyDictionary<string, string[]> Attributes
);
