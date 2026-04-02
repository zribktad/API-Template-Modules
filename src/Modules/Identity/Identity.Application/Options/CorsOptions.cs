using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Identity.Application.Options;

/// <summary>
/// Configuration for the CORS policy, listing the origins that are permitted to make cross-origin requests.
/// </summary>
public sealed class CorsOptions
{
    [Description("List of allowed browser origins for cross-origin requests.")]
    [Required]
    public string[] AllowedOrigins { get; init; } = [];
}
