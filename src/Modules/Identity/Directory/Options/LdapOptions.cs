using System.ComponentModel.DataAnnotations;

namespace Identity.Directory.Options;

/// <summary>
///     Configuration options for Native LDAP connectivity via System.DirectoryServices.Protocols.
/// </summary>
public sealed class LdapOptions
{
    [Required]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 389;

    public bool UseSsl { get; set; }
    public bool ValidateCertificate { get; set; } = true;

    [Required]
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    ///     Distinguished Name of the technical account used for searching users.
    ///     Leave empty for anonymous bind if supported by the server.
    /// </summary>
    public string? BindDn { get; set; }

    /// <summary>
    ///     Password for the BindDn account.
    /// </summary>
    public string? BindPassword { get; set; }

    /// <summary>
    ///     Filter used for user lookups. {0} is replaced by the username.
    ///     Default is common for Active Directory.
    /// </summary>
    public string UserSearchFilter { get; set; } = "(&(objectClass=user)(sAMAccountName={0}))";

    /// <summary>
    ///     Domain used for provisioning local users if the LDAP entry lacks an email address.
    /// </summary>
    public string FallbackEmailDomain { get; set; } = "ldap.local";

    /// <summary>
    ///     Default tenant identifier for provisioned LDAP users.
    ///     Defaults to the Bootstrap tenant (00000000-0000-0000-0000-000000000001).
    /// </summary>
    public Guid DefaultTenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
