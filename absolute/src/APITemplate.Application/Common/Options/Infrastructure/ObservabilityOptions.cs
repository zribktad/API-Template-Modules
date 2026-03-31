namespace APITemplate.Application.Common.Options.Infrastructure;

/// <summary>
/// Root configuration object for observability (tracing, metrics, and logging) exporters and endpoints.
/// </summary>
public sealed class ObservabilityOptions
{
    public OtlpEndpointOptions Otlp { get; init; } = new();

    public AspireEndpointOptions Aspire { get; init; } = new();

    public ObservabilityExportersOptions Exporters { get; init; } = new();
}

/// <summary>
/// Endpoint configuration for the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public sealed class OtlpEndpointOptions
{
    public string Endpoint { get; init; } = string.Empty;
}

/// <summary>
/// Endpoint configuration for the .NET Aspire dashboard exporter.
/// </summary>
public sealed class AspireEndpointOptions
{
    public string Endpoint { get; init; } = string.Empty;
}

/// <summary>
/// Groups the enabled/disabled state for each supported observability exporter.
/// </summary>
public sealed class ObservabilityExportersOptions
{
    public ObservabilityExporterToggleOptions Aspire { get; init; } = new();

    public ObservabilityExporterToggleOptions Otlp { get; init; } = new();

    public ObservabilityExporterToggleOptions Console { get; init; } = new();
}

/// <summary>
/// A simple toggle that enables or disables an individual observability exporter.
/// When <see langword="null"/>, the exporter state falls back to the runtime default.
/// </summary>
public sealed class ObservabilityExporterToggleOptions
{
    public bool? Enabled { get; init; }
}
