using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Options;

/// <summary>
///     Top-level application options that apply globally across the service.
/// </summary>
public sealed class AppOptions : IModuleOptions
{
    public static string SectionName => "App";

    [Description("Logical service name used in telemetry, diagnostics, and application metadata.")]
    [Required]
    [MinLength(1)]
    public string ServiceName { get; init; } = "APITemplate";
}
