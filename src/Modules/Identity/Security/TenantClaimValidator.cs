using System.Security.Claims;
using Identity.Logging;
using Identity.Security.Keycloak;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace Identity.Security;

/// <summary>
///     Validates tenant-related claims after JWT/OIDC token validation and normalizes
///     Keycloak claims into standard .NET claim types used by authorization policies.
/// </summary>
public static class TenantClaimValidator
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
            msg => context.Fail(msg),
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
            msg => context.Fail(msg),
            "OIDC",
            context.HttpContext.RequestAborted
        );
    }

    private static async Task ValidateTokenAsync(
        ClaimsPrincipal? principal,
        HttpContext httpContext,
        Action<string> fail,
        string scheme,
        CancellationToken ct
    )
    {
        if (principal?.Identity is ClaimsIdentity identity)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        bool isServiceAccount = IsServiceAccount(principal);

        if (!isServiceAccount)
            await TryProvisionUserAsync(httpContext, principal, ct);

        if (!HasValidTenantClaim(principal) && !isServiceAccount)
        {
            GetLogger(httpContext).MissingRequiredTenantClaimOnToken(
                AuthConstants.Claims.TenantId,
                scheme
            );
            fail($"Missing required {AuthConstants.Claims.TenantId} claim.");
        }
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

    private static async Task TryProvisionUserAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        CancellationToken ct
    )
    {
        try
        {
            string? sub = principal?.FindFirstValue(AuthConstants.Claims.Subject);
            string? email = principal?.FindFirstValue(ClaimTypes.Email);
            string? username = principal?.FindFirstValue(AuthConstants.Claims.PreferredUsername);

            if (
                string.IsNullOrEmpty(sub)
                || string.IsNullOrEmpty(email)
                || string.IsNullOrEmpty(username)
            )
                return;

            IUserProvisioningService provisioningService =
                httpContext.RequestServices.GetRequiredService<IUserProvisioningService>();

            await provisioningService.ProvisionIfNeededAsync(sub, email, username, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GetLogger(httpContext).UserProvisioningFailedDuringTokenValidation(ex);
        }
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
            .CreateLogger(nameof(TenantClaimValidator));
    }
}
