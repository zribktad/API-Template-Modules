using System.Text.Json.Serialization;

namespace Identity.Auth.Security.Keycloak;

/// <summary>Represents the token response body returned by the Keycloak token endpoint.</summary>
public sealed record KeycloakTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_expires_in")] int? RefreshExpiresIn = null
);
