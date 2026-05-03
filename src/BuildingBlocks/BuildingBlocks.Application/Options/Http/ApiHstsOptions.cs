using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Options.Http;

/// <summary>
///     Configures HTTP Strict Transport Security (HSTS) settings for the API host.
/// </summary>
public sealed class ApiHstsOptions
{
    /// <summary>
    ///     Enables HSTS preloading, allowing the domain to be included in browser preload lists.
    ///     Caution: This is effectively irreversible once submitted.
    /// </summary>
    public bool Preload { get; set; } = false;

    /// <summary>
    ///     Includes subdomains in the HSTS policy.
    /// </summary>
    public bool IncludeSubDomains { get; set; } = true;

    /// <summary>
    ///     The maximum age in days for the HSTS policy.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxAgeDays { get; set; } = 365;
}

