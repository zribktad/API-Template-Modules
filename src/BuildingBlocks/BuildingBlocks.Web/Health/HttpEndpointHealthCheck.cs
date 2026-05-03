using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Web.Health;

/// <summary>
///     Probes an HTTP endpoint with a configurable timeout, returning Healthy on 2xx
///     or Unhealthy on non-success status / exception.
/// </summary>
public abstract class HttpEndpointHealthCheck : IHealthCheck
{
    private static readonly TimeSpan _checkTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly string _httpClientName;
    private readonly string _serviceName;

    protected HttpEndpointHealthCheck(
        IHttpClientFactory httpClientFactory,
        string endpoint,
        string httpClientName,
        string serviceName
    )
    {
        _httpClientFactory = httpClientFactory;
        _endpoint = endpoint;
        _httpClientName = httpClientName;
        _serviceName = serviceName;
    }

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
            cts.CancelAfter(_checkTimeout);

            using HttpClient httpClient = _httpClientFactory.CreateClient(_httpClientName);
            using HttpResponseMessage response = await httpClient.GetAsync(_endpoint, cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"{_serviceName} returned {(int)response.StatusCode}"
                );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} is not reachable", ex);
        }
    }
}

