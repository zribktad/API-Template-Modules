using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options;

/// <summary>
/// Configuration for the default tenant that is seeded when the application bootstraps for the first time.
/// </summary>
public sealed class BootstrapTenantOptions
{
    [Description("Code of the tenant that is seeded automatically during first-time bootstrap.")]
    [Required]
    [MinLength(1)]
    public string Code { get; init; } = "default";

    [Description(
        "Human-readable name of the tenant that is seeded automatically during first-time bootstrap."
    )]
    [Required]
    [MinLength(1)]
    public string Name { get; init; } = "Default Tenant";
}
