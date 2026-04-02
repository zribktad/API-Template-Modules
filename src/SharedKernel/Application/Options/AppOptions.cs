using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options;

/// <summary>
/// Top-level application options that apply globally across the service.
/// </summary>
public sealed class AppOptions
{
    [Description("Logical service name used in telemetry, diagnostics, and application metadata.")]
    [Required]
    [MinLength(1)]
    public string ServiceName { get; init; } = "APITemplate";
}
