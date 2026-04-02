using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedKernel.Infrastructure.Health;

/// <summary>
/// Probes the Keycloak OpenID Connect discovery endpoint with a 5-second timeout.
/// </summary>
public sealed class KeycloakHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _discoveryUrl;

    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        KeycloakHealthCheckOptions options
    )
    {
        _httpClientFactory = httpClientFactory;
        _discoveryUrl = options.DiscoveryUrl;
    }

    /// <summary>
    /// Issues an HTTP GET to the Keycloak discovery URL and returns <see cref="HealthCheckResult.Healthy"/>
    /// on a 2xx response, or <see cref="HealthCheckResult.Unhealthy"/> on a non-success status or exception.
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
                nameof(KeycloakHealthCheck)
            );
            HttpResponseMessage response = await httpClient.GetAsync(_discoveryUrl, cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Keycloak returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Keycloak is not reachable", ex);
        }
    }
}
