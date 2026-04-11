using System.Security.Claims;
using Identity.Features.User;
using Identity.Logging;
using Identity.Security.Keycloak;
using Identity.Security.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace Identity.Security;

/// <summary>
///     Runs immediately after the ASP.NET Core JWT Bearer or OpenID Connect handler has successfully
///     validated the token (signature, lifetime, issuer). It is wired from
///     <see cref="Identity.IdentityModule" /> to <c>JwtBearerEvents.OnTokenValidated</c> and
///     <c>OpenIdConnectEvents.OnTokenValidated</c>.
/// </summary>
/// <remarks>
///     <para>
///         Execution order for each request:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///                 Map Keycloak-specific claims (realm roles, username, etc.) onto standard .NET
///                 <see cref="Claim" /> types so authorization policies and app code see a stable shape.
///             </description>
///         </item>
///         <item>
///             <description>
///                 If the principal looks like a human user (not a Keycloak service account), resolve
///                 local access: invitation accepted, user row, tenant linkage via
///                 <see cref="ResolveAppUserAccessQuery" />. Failure calls the handler-supplied
///                 fail callback and stops authentication.
///             </description>
///         </item>
///         <item>
///             <description>
///                 For human users, require a non-empty GUID <c>tenant_id</c> claim. Service accounts
///                 skip both user resolution and this check so machine-to-machine callers are not
///                 tied to the invitation flow.
///             </description>
///         </item>
///     </list>
///     <para>
///         <see cref="SetAccessDeniedItems" /> stores machine-readable codes on
///         <see cref="HttpContext.Items" /> so middleware such as OIDC redirects can show a
///         consistent error page when authentication is failed from this pipeline.
///     </para>
/// </remarks>
public static class IdentityTokenValidatedPipeline
{
    /// <summary>
    ///     JWT Bearer entry point (API / bearer tokens). Delegates to the shared validation routine
    ///     with scheme label <c>JWT Bearer</c> for logging.
    /// </summary>
    public static Task OnTokenValidated(JwtTokenValidatedContext context)
    {
        return ValidateTokenAsync(
            context.Principal,
            context.HttpContext,
            context.Fail,
            "JWT Bearer",
            context.HttpContext.RequestAborted
        );
    }

    /// <summary>
    ///     OpenID Connect entry point (BFF cookie sign-in and refresh paths that use the OIDC
    ///     handler). Same rules as <see cref="OnTokenValidated(JwtTokenValidatedContext)" />;
    ///     scheme label <c>OIDC</c> distinguishes logs.
    /// </summary>
    public static Task OnTokenValidated(OidcTokenValidatedContext context)
    {
        return ValidateTokenAsync(
            context.Principal,
            context.HttpContext,
            context.Fail,
            "OIDC",
            context.HttpContext.RequestAborted
        );
    }

    /// <summary>
    ///     Shared implementation for both JWT Bearer and OIDC: map claims, optionally resolve the
    ///     app user, then enforce <c>tenant_id</c> for human principals.
    /// </summary>
    /// <param name="principal">User identity produced by the handler; may be augmented by claim mapping.</param>
    /// <param name="httpContext">Current request; used for DI, logging, and access-denied metadata.</param>
    /// <param name="fail">
    ///     Callback into the JWT/OIDC <c>TokenValidatedContext</c> to mark authentication as failed.
    /// </param>
    /// <param name="scheme">Human-readable scheme name for structured logs only.</param>
    /// <param name="ct">Forwarded from the HTTP request for cooperative cancellation.</param>
    private static async Task ValidateTokenAsync(
        ClaimsPrincipal? principal,
        HttpContext httpContext,
        Action<Exception> fail,
        string scheme,
        CancellationToken ct
    )
    {
        // Step 1 — Normalize claims from Keycloak into the claim types the rest of the app expects.
        if (principal?.Identity is ClaimsIdentity identity)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        bool isServiceAccount = IsServiceAccount(principal);

        // Step 2 — Humans must exist in our DB with a valid invitation/tenant; service accounts skip this.
        if (!isServiceAccount)
        {
            bool continuePipeline = await TryResolveHumanUserAsync(
                httpContext,
                principal,
                fail,
                ct
            );
            if (!continuePipeline)
                return;
        }

        // Step 3 — Every human session must carry a tenant scope in the token; empty GUID is invalid.
        if (!HasValidTenantClaim(principal) && !isServiceAccount)
        {
            GetLogger(httpContext)
                .MissingRequiredTenantClaimOnToken(AuthConstants.Claims.TenantId, scheme);
            SetAccessDeniedItems(
                httpContext,
                UserAccessErrorCodes.MissingTenantClaim,
                UserAccessDeniedMessages.MissingTenantClaim
            );
            fail(
                new UserAccessDeniedException(
                    UserAccessErrorCodes.MissingTenantClaim,
                    UserAccessDeniedMessages.MissingTenantClaim
                )
            );
        }
    }

    /// <summary>
    ///     Ensures the token carries the minimum profile claims and that the person is allowed into
    ///     the application (invitation accepted, provisioning succeeded).
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> if authentication must stop (<paramref name="fail" /> already invoked).
    /// </returns>
    private static async Task<bool> TryResolveHumanUserAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        Action<Exception> fail,
        CancellationToken ct
    )
    {
        string? sub = principal?.FindFirstValue(AuthConstants.Claims.Subject);
        string? email = principal?.FindFirstValue(ClaimTypes.Email);
        string? username = principal?.FindFirstValue(AuthConstants.Claims.PreferredUsername);

        // Without OIDC subject + email + username we cannot correlate to Keycloak or our user store.
        if (
            string.IsNullOrEmpty(sub)
            || string.IsNullOrEmpty(email)
            || string.IsNullOrEmpty(username)
        )
        {
            SetAccessDeniedItems(
                httpContext,
                UserAccessErrorCodes.MissingProfileClaims,
                UserAccessDeniedMessages.MissingProfileClaims
            );
            fail(
                new UserAccessDeniedException(
                    UserAccessErrorCodes.MissingProfileClaims,
                    UserAccessDeniedMessages.MissingProfileClaims
                )
            );
            return false;
        }

        try
        {
            IMessageBus bus = httpContext.RequestServices.GetRequiredService<IMessageBus>();

            // Synchronous handler resolves invitation, tenant membership, and provisioning in one shot.
            UserAccessResolution resolution = await bus.InvokeAsync<UserAccessResolution>(
                new ResolveAppUserAccessQuery(sub, email, username),
                ct
            );

            if (!resolution.IsAllowed)
            {
                string code = resolution.ErrorCode ?? UserAccessErrorCodes.NoInvitation;
                string message = resolution.Message ?? UserAccessDeniedMessages.NoInvitation;
                SetAccessDeniedItems(httpContext, code, message);
                fail(new UserAccessDeniedException(code, message));
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GetLogger(httpContext).UserProvisioningFailedDuringTokenValidation(ex);
            fail(ex);
            return false;
        }
    }

    /// <summary>
    ///     Surfaces a stable error code and message to later pipeline stages (e.g. OIDC redirect
    ///     handlers) via <see cref="HttpContext.Items" />.
    /// </summary>
    private static void SetAccessDeniedItems(
        HttpContext httpContext,
        string errorCode,
        string detail
    )
    {
        httpContext.Items[AuthConstants.HttpContextItems.AccessDeniedErrorCode] = errorCode;
        httpContext.Items[AuthConstants.HttpContextItems.AccessDeniedDetail] = detail;
    }

    /// <summary>
    ///     Returns whether the principal has a <c>tenant_id</c> claim whose value parses to a
    ///     non-empty <see cref="Guid" />.
    /// </summary>
    public static bool HasValidTenantClaim(ClaimsPrincipal? principal)
    {
        return principal?.HasClaim(c =>
                c.Type == AuthConstants.Claims.TenantId
                && Guid.TryParse(c.Value, out Guid tenantId)
                && tenantId != Guid.Empty
            ) == true;
    }

    /// <summary>
    ///     Keycloak encodes client credentials / service users with a <c>preferred_username</c>
    ///     prefix; those principals skip tenant and invitation rules meant for interactive users.
    /// </summary>
    private static bool IsServiceAccount(ClaimsPrincipal? principal)
    {
        string? username = principal?.FindFirstValue(AuthConstants.Claims.PreferredUsername);
        return username != null
            && username.StartsWith(
                AuthConstants.Claims.ServiceAccountUsernamePrefix,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static ILogger GetLogger(HttpContext httpContext)
    {
        return httpContext
            .RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentityTokenValidatedPipeline));
    }
}
