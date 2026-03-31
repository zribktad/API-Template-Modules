using System.Text.Json.Serialization;

namespace Identity.Infrastructure.Security.Keycloak;

/// <summary>Represents the token response body returned by the Keycloak token endpoint.</summary>
public sealed record KeycloakTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn
);
