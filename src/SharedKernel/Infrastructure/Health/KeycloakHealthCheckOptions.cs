using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Infrastructure.Health;

/// <summary>
///     Options that configure the <see cref="KeycloakHealthCheck" /> with the OpenID Connect discovery
///     URL to probe when verifying Keycloak availability.
/// </summary>
public sealed class KeycloakHealthCheckOptions
{
    [Description("OpenID Connect discovery endpoint used by the Keycloak health probe.")]
    [Required]
    [Url]
    public string DiscoveryUrl { get; init; } = string.Empty;
}
