using Microsoft.Extensions.Options;

namespace BuildingBlocks.Web.Health;

/// <summary>
///     Probes the OTLP collector endpoint with a 5-second timeout.
/// </summary>
public sealed class OtlpCollectorHealthCheck : HttpEndpointHealthCheck
{
    public OtlpCollectorHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<OtlpCollectorHealthCheckOptions> options
    )
        : base(
            httpClientFactory,
            options.Value.Endpoint,
            nameof(OtlpCollectorHealthCheck),
            "OTLP collector"
        ) { }
}

