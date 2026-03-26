namespace APITemplate.Application.Common.Security;

/// <summary>
/// Shared constants for authentication, OpenID Connect, and OAuth2 token payload names.
/// </summary>
public static class AuthConstants
{
    /// <summary>Named HTTP client identifiers for Keycloak communication.</summary>
    public static class HttpClients
    {
        public const string KeycloakToken = "KeycloakTokenClient";
        public const string KeycloakAdmin = "KeycloakAdminClient";
    }

    /// <summary>Relative path segments for the Keycloak OpenID Connect endpoints.</summary>
    public static class OpenIdConnect
    {
        public const string AuthorizationEndpointPath = "protocol/openid-connect/auth";
        public const string TokenEndpointPath = "protocol/openid-connect/token";
    }

    /// <summary>OpenAPI / Scalar UI security scheme and client identifiers.</summary>
    public static class OpenApi
    {
        public const string OAuth2Scheme = "OAuth2";
        public const string ScalarClientId = "api-template-scalar";
    }

    /// <summary>Standard OAuth2 / OIDC scope names requested during authentication.</summary>
    public static class Scopes
    {
        public const string OpenId = "openid";
        public const string Profile = "profile";
        public const string Email = "email";

        public static readonly string[] Default = [OpenId, Profile, Email];
    }

    /// <summary>Cookie field names used to persist session token data in the BFF layer.</summary>
    public static class CookieTokenNames
    {
        public const string AccessToken = "access_token";
        public const string RefreshToken = "refresh_token";
        public const string ExpiresAt = "expires_at";
        public const string ExpiresIn = "expires_in";
    }

    /// <summary>Form parameter names used in OAuth2 token endpoint requests.</summary>
    public static class OAuth2FormParameters
    {
        public const string GrantType = "grant_type";
        public const string ClientId = "client_id";
        public const string ClientSecret = "client_secret";
        public const string RefreshToken = "refresh_token";
    }

    /// <summary>OAuth2 grant type string values used in token requests.</summary>
    public static class OAuth2GrantTypes
    {
        public const string ClientCredentials = "client_credentials";
        public const string RefreshToken = "refresh_token";
    }

    /// <summary>Keycloak required-action identifiers sent during user lifecycle operations.</summary>
    public static class KeycloakActions
    {
        public const string VerifyEmail = "VERIFY_EMAIL";
        public const string UpdatePassword = "UPDATE_PASSWORD";
    }

    /// <summary>JWT claim names used to extract identity and role information from tokens.</summary>
    public static class Claims
    {
        public const string Subject = "sub";
        public const string RealmAccess = "realm_access";
        public const string Roles = "roles";
        public const string PreferredUsername = "preferred_username";
        public const string ServiceAccountUsernamePrefix = "service-account-";
        public const string TenantId = "tenant_id";
    }

    /// <summary>
    /// Constants for the custom CSRF header contract used by <c>CsrfValidationMiddleware</c>.
    /// </summary>
    /// <remarks>
    /// SPAs retrieve these values at runtime via <c>GET /api/v1/bff/csrf</c> and must send
    /// <c>X-CSRF: 1</c> on every non-safe (mutating) request authenticated with a session cookie.
    /// </remarks>
    public static class Csrf
    {
        /// <summary>Name of the required anti-CSRF request header.</summary>
        public const string HeaderName = "X-CSRF";

        /// <summary>Expected value of the anti-CSRF header.</summary>
        public const string HeaderValue = "1";
    }

    /// <summary>Authentication scheme names registered for the BFF cookie and OIDC flows.</summary>
    public static class BffSchemes
    {
        public const string Cookie = "BffCookie";
        public const string Oidc = "BffOidc";
    }

    /// <summary>Named authorization policy identifiers registered in the ASP.NET Core policy store.</summary>
    public static class Policies
    {
        public const string PlatformAdmin = "PlatformAdmin";
        public const string TenantAdmin = "TenantAdmin";
    }
}
