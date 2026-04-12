using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Identity.Auth.Options;

/// <summary>
///     Configuration for the Keycloak identity provider, covering realm, server URL, client credentials,
///     and startup readiness-check behaviour.
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

    [Description(
        "Confidential client used only by this API to verify the current password (resource-owner grant)."
    )]
    [ConfigurationKeyName("passwordVerification")]
    public KeycloakPasswordVerificationOptions PasswordVerification { get; init; } = new();

    [Description("Shared secret for inbound Keycloak event webhook calls.")]
    [ConfigurationKeyName("eventWebhook")]
    public KeycloakEventWebhookOptions EventWebhook { get; init; } = new();
}

/// <summary>
///     Client-secret credentials used when authenticating against the Keycloak Admin REST API.
/// </summary>
public sealed class KeycloakCredentialsOptions
{
    [Description("OIDC client secret used when calling Keycloak administrative endpoints.")]
    [ConfigurationKeyName("secret")]
    public string Secret { get; init; } = string.Empty;
}

/// <summary>
///     Credentials for the confidential Keycloak client that enables password verification via the token endpoint.
/// </summary>
public sealed class KeycloakPasswordVerificationOptions
{
    [ConfigurationKeyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    [ConfigurationKeyName("clientSecret")]
    public string ClientSecret { get; init; } = string.Empty;
}

/// <summary>Inbound webhook authentication (Keycloak HTTP event listener → API).</summary>
public sealed class KeycloakEventWebhookOptions
{
    /// <summary>Default for <see cref="ApiKeyHeaderName"/> when configuration omits or blanks the header name.</summary>
    public const string DefaultApiKeyHeaderName = "X-Keycloak-Event-Key";

    /// <summary>When non-empty, the request header named <see cref="ApiKeyHeaderName"/> must equal this value.</summary>
    [ConfigurationKeyName("apiKey")]
    public string ApiKey { get; init; } = string.Empty;

    [ConfigurationKeyName("apiKeyHeaderName")]
    public string ApiKeyHeaderName { get; init; } = DefaultApiKeyHeaderName;
}
