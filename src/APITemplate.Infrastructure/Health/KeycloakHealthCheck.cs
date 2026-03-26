using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Health;

/// <summary>
/// ASP.NET Core health check that verifies Keycloak availability by probing its
/// OpenID Connect discovery endpoint with a 5-second timeout.
/// </summary>
public sealed class KeycloakHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly string _discoveryUrl;

    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions
    )
    {
        _httpClient = httpClientFactory.CreateClient(nameof(KeycloakHealthCheck));
        var keycloak = keycloakOptions.Value;
        _discoveryUrl = KeycloakUrlHelper.BuildDiscoveryUrl(keycloak.AuthServerUrl, keycloak.Realm);
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);

            var response = await _httpClient.GetAsync(_discoveryUrl, cts.Token);
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
