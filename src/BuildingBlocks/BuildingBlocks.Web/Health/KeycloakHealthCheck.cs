using Microsoft.Extensions.Options;

namespace BuildingBlocks.Web.Health;

/// <summary>
///     Probes the Keycloak OpenID Connect discovery endpoint with a 5-second timeout.
/// </summary>
public sealed class KeycloakHealthCheck : HttpEndpointHealthCheck
{
    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakHealthCheckOptions> options
    )
        : base(
            httpClientFactory,
            options.Value.DiscoveryUrl,
            nameof(KeycloakHealthCheck),
            "Keycloak"
        ) { }
}

