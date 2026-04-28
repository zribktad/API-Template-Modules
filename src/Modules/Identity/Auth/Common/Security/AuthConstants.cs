using SharedKernel.Contracts.Security;

namespace Identity.Auth.Security;

/// <summary>
///     Shared constants for authentication, OpenID Connect, and OAuth2 token payload names.
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
        public const string OAuth2ScalarScheme = "OAuth2-Scalar";
        public const string OAuth2PublicScheme = "OAuth2-Public";
        public const string ScalarClientId = "api-template-scalar";
        public const string PublicClientId = "api-template-public";
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
        public const string IdToken = "id_token";
        public const string ExpiresAt = "expires_at";
        public const string ExpiresIn = "expires_in";
        public const string LastValidated = ".last_validated";
    }

    /// <summary>Form parameter names used in OAuth2 token endpoint requests.</summary>
    public static class OAuth2FormParameters
    {
        public const string GrantType = "grant_type";
        public const string ClientId = "client_id";
        public const string ClientSecret = "client_secret";
        public const string RefreshToken = "refresh_token";
        public const string Username = "username";
        public const string Password = "password";
    }

    /// <summary>OAuth2 error codes returned in token endpoint responses.</summary>
    public static class OAuth2Errors
    {
        public const string InvalidGrant = "invalid_grant";
    }

    /// <summary>OAuth2 grant type string values used in token requests.</summary>
    public static class OAuth2GrantTypes
    {
        public const string ClientCredentials = "client_credentials";
        public const string RefreshToken = "refresh_token";
        public const string Password = "password";
    }

    /// <summary>Keycloak required-action identifiers sent during user lifecycle operations.</summary>
    public static class KeycloakActions
    {
        public const string VerifyEmail = "VERIFY_EMAIL";
        public const string UpdatePassword = "UPDATE_PASSWORD";
    }

    /// <summary>Keycloak credential <c>type</c> values for Admin API password operations.</summary>
    public static class KeycloakCredentialTypes
    {
        public const string Password = "password";
    }

    /// <summary>JWT claim names used to extract identity and role information from tokens.</summary>
    public static class Claims
    {
        public const string Subject = "sub";
        public const string RealmAccess = "realm_access";
        public const string Roles = "roles";
        public const string PreferredUsername = "preferred_username";

        /// <summary>Application permission strings expanded onto the principal by <c>IClaimsTransformation</c>.</summary>
        public const string Permission = "Permission";

        /// <summary>OAuth 2.0 <c>azp</c> (authorized party) — present on many Keycloak access tokens.</summary>
        public const string AuthorizedParty = "azp";
        public const string ServiceAccountUsernamePrefix = "service-account-";
        public const string TenantId = TenantSecurityClaims.TenantId;
    }

    /// <summary>
    ///     Constants for the custom CSRF header contract used by <c>CsrfValidationMiddleware</c>.
    /// </summary>
    /// <remarks>
    ///     After login, SPAs call <c>GET /api/v1/bff/csrf</c> with the BFF cookie; the response
    ///     includes a Data Protection–bound <c>headerValue</c>. Send that value on every non-safe
    ///     request when using cookie auth. Without a session the endpoint returns <c>401</c>.
    /// </remarks>
    public static class Csrf
    {
        /// <summary>Name of the required anti-CSRF request header.</summary>
        public const string HeaderName = "X-CSRF";

        /// <summary><c>tokenFormat</c> values returned by <c>GET /bff/csrf</c> when authenticated.</summary>
        public static class TokenFormats
        {
            public const string DataProtection = "DataProtection";
        }
    }

    /// <summary>
    ///     Keycloak-specific authentication properties keys used during OIDC challenge construction.
    /// </summary>
    public static class KeycloakAuthProperties
    {
        /// <summary>
        ///     Authentication properties key that carries the Keycloak identity provider alias.
        ///     Keycloak reads this value and immediately redirects the user to the specified IdP
        ///     (e.g. Google, GitHub), bypassing the Keycloak login page entirely.
        ///     Must match the <em>Alias</em> configured in Keycloak under Identity Providers.
        /// </summary>
        public const string IdpHint = "kc_idp_hint";
    }

    /// <summary>Well-known BFF endpoint paths referenced outside the routing layer.</summary>
    public static class BffRoutes
    {
        public const string Logout = "/api/v1/bff/logout";
    }

    /// <summary>Authentication properties keys used internally by the BFF session layer.</summary>
    public static class SessionProperties
    {
        /// <summary>
        ///     Properties key that carries the server-side BFF session identifier so downstream
        ///     event handlers can correlate the cookie with its session record.
        /// </summary>
        public const string SessionId = ".bff.sessionId";
    }

    /// <summary>Authentication scheme names registered for the BFF cookie and OIDC flows.</summary>
    public static class BffSchemes
    {
        public const string Cookie = "BffCookie";
        public const string Oidc = "BffOidc";
    }

    /// <summary>Authentication scheme names for inbound webhooks authenticated by shared secret.</summary>
    public static class WebhookSchemes
    {
        public const string KeycloakEvent = "KeycloakEventWebhook";
    }

    /// <summary>Named authorization policy identifiers registered in the ASP.NET Core policy store.</summary>
    public static class Policies
    {
        public const string PlatformAdmin = "PlatformAdmin";
        public const string TenantAdmin = "TenantAdmin";
    }

    /// <summary>
    ///     Keys on <c>HttpContext.Items</c> when authentication succeeds at the IdP but the application
    ///     rejects the principal (used by JWT <c>OnChallenge</c> to emit ProblemDetails).
    /// </summary>
    public static class HttpContextItems
    {
        public const string AccessDeniedErrorCode = "Identity.AccessDenied.ErrorCode";
        public const string AccessDeniedDetail = "Identity.AccessDenied.Detail";
    }

    /// <summary>
    ///     Keys and TTLs for <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" />
    ///     entries written by the Identity auth layer.
    /// </summary>
    public static class DistributedCache
    {
        /// <summary>
        ///     Prefix for a positive user-access resolution for a Keycloak <c>sub</c>. Full key: prefix +
        ///     subject string.
        /// </summary>
        public const string UserAccessAllowedKeyPrefix = "identity:useraccess:allowed:v1:";

        /// <summary>
        ///     Marker byte written to the cache under <see cref="UserAccessAllowedKeyPrefix" /> +
        ///     Keycloak <c>sub</c> after a successful user-access resolution.
        /// </summary>
        public const byte UserAccessAllowedPayloadMarker = 1;

        /// <summary>
        ///     How long a successful access resolution may be reused without hitting the app database.
        ///     Denied outcomes are not cached so invitation and account state changes propagate quickly.
        /// </summary>
        public static readonly TimeSpan UserAccessAllowedTtl = TimeSpan.FromMinutes(2);

        /// <summary>Prefix for cached permission lists keyed by OIDC <c>sub</c> or app user id string.</summary>
        public const string UserPermissionsKeyPrefix = "UserPermissions:";

        /// <summary>Builds the distributed cache key used for expanded permission claims.</summary>
        public static string UserPermissionsCacheKey(string subjectOrUserId) =>
            $"{UserPermissionsKeyPrefix}{subjectOrUserId}";

        /// <summary>TTL for cached permission lists loaded from the Identity database.</summary>
        public static readonly TimeSpan UserPermissionsTtl = TimeSpan.FromMinutes(10);
    }
}
