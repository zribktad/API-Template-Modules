using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace SharedKernel.Application.Options.Infrastructure;

/// <summary>
///     Root configuration object for observability (tracing, metrics, and logging) exporters and endpoints.
/// </summary>
public sealed class ObservabilityOptions
{
    [Description("Endpoint configuration for OpenTelemetry OTLP export.")]
    [Required]
    [ValidateObjectMembers]
    public OtlpEndpointOptions Otlp { get; init; } = new();

    [Description("Endpoint configuration for .NET Aspire dashboard export.")]
    [Required]
    [ValidateObjectMembers]
    public AspireEndpointOptions Aspire { get; init; } = new();

    [Description("Enabled-state toggles for supported observability exporters.")]
    [Required]
    [ValidateObjectMembers]
    public ObservabilityExportersOptions Exporters { get; init; } = new();

    /// <summary>
    ///     Resolves the active OTLP endpoint based on exporter configuration.
    ///     Prefers an explicit OTLP endpoint when enabled; falls back to the Aspire endpoint
    ///     when Aspire is explicitly enabled or when <paramref name="isDevelopment" /> is <c>true</c>
    ///     and no explicit toggle is set.
    /// </summary>
    public Uri? ResolveOtlpEndpoint(bool isDevelopment = false)
    {
        if (
            Exporters.Otlp.Enabled == true
            && Uri.TryCreate(Otlp.Endpoint, UriKind.Absolute, out Uri? otlpUri)
        )
            return otlpUri;

        bool aspireEnabled = Exporters.Aspire.Enabled ?? isDevelopment;
        if (
            aspireEnabled
            && Uri.TryCreate(Aspire.Endpoint, UriKind.Absolute, out Uri? aspireUri)
        )
            return aspireUri;

        return null;
    }
}

/// <summary>
///     Endpoint configuration for the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public sealed class OtlpEndpointOptions
{
    [Description("Absolute OTLP endpoint URI used when OTLP export is enabled.")]
    public string Endpoint { get; init; } = string.Empty;
}

/// <summary>
///     Endpoint configuration for the .NET Aspire dashboard exporter.
/// </summary>
public sealed class AspireEndpointOptions
{
    [Description("Absolute Aspire dashboard endpoint URI used when Aspire export is enabled.")]
    public string Endpoint { get; init; } = string.Empty;
}

/// <summary>
///     Groups the enabled/disabled state for each supported observability exporter.
/// </summary>
public sealed class ObservabilityExportersOptions
{
    [Description("Toggle controlling Aspire exporter activation.")]
    [Required]
    [ValidateObjectMembers]
    public ObservabilityExporterToggleOptions Aspire { get; init; } = new();

    [Description("Toggle controlling OTLP exporter activation.")]
    [Required]
    [ValidateObjectMembers]
    public ObservabilityExporterToggleOptions Otlp { get; init; } = new();

    [Description("Toggle controlling console exporter activation.")]
    [Required]
    [ValidateObjectMembers]
    public ObservabilityExporterToggleOptions Console { get; init; } = new();
}

/// <summary>
///     A simple toggle that enables or disables an individual observability exporter.
///     When <see langword="null" />, the exporter state falls back to the runtime default.
/// </summary>
public sealed class ObservabilityExporterToggleOptions
{
    [Description(
        "Explicit enabled-state override for a single exporter; null means runtime default."
    )]
    public bool? Enabled { get; init; }
}
