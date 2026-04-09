using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace SharedKernel.Infrastructure.Health;

/// <summary>
///     Probes the OTLP collector endpoint with a 5-second timeout.
/// </summary>
public sealed class OtlpCollectorHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private readonly string _endpoint;

    private readonly IHttpClientFactory _httpClientFactory;

    public OtlpCollectorHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<OtlpCollectorHealthCheckOptions> options
    )
    {
        _httpClientFactory = httpClientFactory;
        _endpoint = options.Value.Endpoint;
    }

    /// <summary>
    ///     Issues an HTTP GET to the OTLP collector endpoint and returns <see cref="HealthCheckResult.Healthy" />
    ///     on a 2xx response, or <see cref="HealthCheckResult.Unhealthy" /> on a non-success status or exception.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            cts.CancelAfter(CheckTimeout);

            using HttpClient httpClient = _httpClientFactory.CreateClient(
                nameof(OtlpCollectorHealthCheck)
            );
            HttpResponseMessage response = await httpClient.GetAsync(_endpoint, cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"OTLP collector returned {(int)response.StatusCode}"
                );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("OTLP collector is not reachable", ex);
        }
    }
}
