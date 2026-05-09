using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace Identity.Auth.Options;

/// <summary>
///     Configuration for the CORS policy, listing the origins that are permitted to make cross-origin requests.
/// </summary>
public sealed class CorsOptions : IModuleOptions
{
    public static string SectionName => "Cors";

    [Description("List of allowed browser origins for cross-origin requests.")]
    [Required]
    public string[] AllowedOrigins { get; init; } = [];
}
