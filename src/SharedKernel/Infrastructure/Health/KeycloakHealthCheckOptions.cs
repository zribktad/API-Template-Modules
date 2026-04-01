namespace SharedKernel.Infrastructure.Health;

/// <summary>
/// Options that configure the <see cref="KeycloakHealthCheck"/> with the OpenID Connect discovery
/// URL to probe when verifying Keycloak availability.
/// </summary>
public sealed class KeycloakHealthCheckOptions
{
    public string DiscoveryUrl { get; init; } = string.Empty;
}
