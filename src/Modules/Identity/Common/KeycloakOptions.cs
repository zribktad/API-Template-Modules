using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Identity.Options;

/// <summary>
/// Configuration for the Keycloak identity provider, covering realm, server URL, client credentials,
/// and startup readiness-check behaviour.
/// </summary>
public sealed class KeycloakOptions
{
    [Description("Keycloak realm used by this application.")]
    [Required]
    [ConfigurationKeyName("realm")]
    public string Realm { get; init; } = string.Empty;

    [Description("Base URL of the Keycloak authentication server.")]
    [Required]
    [ConfigurationKeyName("auth-server-url")]
    public string AuthServerUrl { get; init; } = string.Empty;

    [Description("OIDC client identifier registered in Keycloak.")]
    [ConfigurationKeyName("resource")]
    public string Resource { get; init; } = string.Empty;

    [Description("When true, startup readiness probing against Keycloak is skipped.")]
    [ConfigurationKeyName("SkipReadinessCheck")]
    public bool SkipReadinessCheck { get; init; }

    [Description("Maximum number of readiness probe retries before startup fails.")]
    [Range(1, 100)]
    [ConfigurationKeyName("ReadinessMaxRetries")]
    public int ReadinessMaxRetries { get; init; } = 30;

    [Description("Client credentials used for server-to-server Keycloak administration calls.")]
    [ConfigurationKeyName("credentials")]
    public KeycloakCredentialsOptions Credentials { get; init; } = new();
}

/// <summary>
/// Client-secret credentials used when authenticating against the Keycloak Admin REST API.
/// </summary>
public sealed class KeycloakCredentialsOptions
{
    [Description("OIDC client secret used when calling Keycloak administrative endpoints.")]
    [ConfigurationKeyName("secret")]
    public string Secret { get; init; } = string.Empty;
}

