using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace Identity.Options;

/// <summary>
///     Configuration for the default tenant that is seeded when the application bootstraps for the first time.
/// </summary>
public sealed class BootstrapTenantOptions : IModuleOptions
{
    public static string SectionName => "BootstrapTenant";

    [Description("Code of the tenant that is seeded automatically during first-time bootstrap.")]
    [Required]
    [MinLength(1)]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Code cannot be empty or whitespace only.")]
    public string Code { get; init; } = "default";

    [Description(
        "Human-readable name of the tenant that is seeded automatically during first-time bootstrap."
    )]
    [Required]
    [MinLength(1)]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Name cannot be empty or whitespace only.")]
    public string Name { get; init; } = "Default Tenant";
}
