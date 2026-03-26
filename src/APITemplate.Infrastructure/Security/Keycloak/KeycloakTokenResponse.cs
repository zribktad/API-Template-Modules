using System.Text.Json.Serialization;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>Represents the token response body returned by the Keycloak token endpoint.</summary>
internal sealed record KeycloakTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn
);
