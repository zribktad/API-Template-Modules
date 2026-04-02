using APITemplate.Application.Common.Security;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// Builds well-known Keycloak URL components (authority, discovery, token endpoint) from
/// configured base URL and realm name.
/// </summary>
public static class KeycloakUrlHelper
{
    /// <summary>Returns the realm authority URL: <c>{authServerUrl}/realms/{realm}</c>.</summary>
    public static string BuildAuthority(string authServerUrl, string realm) =>
        $"{authServerUrl.TrimEnd('/')}/realms/{realm}";

    /// <summary>Returns the OpenID Connect discovery document URL for the given realm.</summary>
    public static string BuildDiscoveryUrl(string authServerUrl, string realm) =>
        $"{BuildAuthority(authServerUrl, realm)}/.well-known/openid-configuration";

    /// <summary>Returns the OAuth2 token endpoint URL for the given realm.</summary>
    public static string BuildTokenEndpoint(string authServerUrl, string realm) =>
        $"{BuildAuthority(authServerUrl, realm)}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
}
