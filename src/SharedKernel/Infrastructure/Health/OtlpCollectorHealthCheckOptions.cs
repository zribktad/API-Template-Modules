using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Infrastructure.Health;

/// <summary>
///     Options that configure the <see cref="OtlpCollectorHealthCheck" /> with the endpoint
///     URI to probe when verifying OTLP collector availability.
/// </summary>
public sealed class OtlpCollectorHealthCheckOptions
{
    [Description("Absolute OTLP collector endpoint URI used by the health probe.")]
    [Required]
    [Url]
    public string Endpoint { get; set; } = string.Empty;
}
