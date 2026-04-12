namespace Identity.Auth.Security.Keycloak;

/// <summary>
///     Builds well-known Keycloak URL components (authority, discovery, token endpoint) from
///     configured base URL and realm name.
/// </summary>
public static class KeycloakUrlHelper
{
    /// <summary>Returns the realm authority URL: <c>{authServerUrl}/realms/{realm}</c>.</summary>
    public static string BuildAuthority(string authServerUrl, string realm)
    {
        return $"{authServerUrl.TrimEnd('/')}/realms/{realm}";
    }

    /// <summary>
    ///     OpenID Connect and JWT metadata should require HTTPS in non-dev setups. Returns false when
    ///     <paramref name="authorityUrl" /> uses plain <c>http://</c> (typical local development).
    /// </summary>
    public static bool ShouldRequireHttpsMetadata(string authorityUrl)
    {
        return !authorityUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the OpenID Connect discovery document URL for the given realm.</summary>
    public static string BuildDiscoveryUrl(string authServerUrl, string realm)
    {
        return $"{BuildAuthority(authServerUrl, realm)}/.well-known/openid-configuration";
    }

    /// <summary>Returns the OAuth2 token endpoint URL for the given realm.</summary>
    public static string BuildTokenEndpoint(string authServerUrl, string realm)
    {
        return $"{BuildAuthority(authServerUrl, realm)}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
    }

    /// <summary>
    ///     Admin REST: POST to log out all sessions for a Keycloak user
    ///     (<c>/admin/realms/{realm}/users/{id}/logout</c>).
    /// </summary>
    public static string BuildAdminUserLogoutUrl(
        string authServerUrl,
        string realm,
        string keycloakUserId
    )
    {
        return $"{authServerUrl.TrimEnd('/')}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}/logout";
    }
}
