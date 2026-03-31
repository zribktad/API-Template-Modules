using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options;

/// <summary>
/// Configuration for the default tenant that is seeded when the application bootstraps for the first time.
/// </summary>
public sealed class BootstrapTenantOptions
{
    [Required]
    public string Code { get; init; } = "default";

    [Required]
    public string Name { get; init; } = "Default Tenant";
}
