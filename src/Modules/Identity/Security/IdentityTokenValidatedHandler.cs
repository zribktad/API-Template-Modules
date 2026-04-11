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
///     Validates tenant-related claims after JWT/OIDC token validation and normalizes
///     Keycloak claims into standard .NET claim types used by authorization policies.
/// </summary>
public static class IdentityTokenValidatedHandler
{
    /// <summary>
    ///     JWT Bearer token callback. Maps Keycloak claims, enforces tenant claim presence for user
    ///     tokens, and provisions the local user record on first login.
    /// </summary>
    public static Task OnTokenValidated(JwtTokenValidatedContext context)
    {
        return ValidateTokenAsync(
            context.Principal,
            context.HttpContext,
            ex => context.Fail(ex),
            "JWT Bearer",
            context.HttpContext.RequestAborted
        );
    }

    /// <summary>
    ///     OpenID Connect token callback. Applies the same tenant and claim-mapping rules as JWT Bearer validation.
    /// </summary>
    public static Task OnTokenValidated(OidcTokenValidatedContext context)
    {
        return ValidateTokenAsync(
            context.Principal,
            context.HttpContext,
            ex => context.Fail(ex),
            "OIDC",
            context.HttpContext.RequestAborted
        );
    }

    private static async Task ValidateTokenAsync(
        ClaimsPrincipal? principal,
        HttpContext httpContext,
        Action<Exception> fail,
        string scheme,
        CancellationToken ct
    )
    {
        if (principal?.Identity is ClaimsIdentity identity)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        bool isServiceAccount = IsServiceAccount(principal);

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

    /// <returns><see langword="false" /> when authentication was failed and the pipeline must stop.</returns>
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
    ///     Checks whether the principal has a non-empty GUID value in the <c>tenant_id</c> claim.
    /// </summary>
    public static bool HasValidTenantClaim(ClaimsPrincipal? principal)
    {
        return principal?.HasClaim(c =>
                c.Type == AuthConstants.Claims.TenantId
                && Guid.TryParse(c.Value, out Guid tenantId)
                && tenantId != Guid.Empty
            ) == true;
    }

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
            .CreateLogger(nameof(IdentityTokenValidatedHandler));
    }
}
