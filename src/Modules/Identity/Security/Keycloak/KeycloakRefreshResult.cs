namespace Identity.Security.Keycloak;

public sealed record KeycloakRefreshResult(
    KeycloakRefreshStatus Status,
    KeycloakTokenResponse? TokenResponse = null
);
