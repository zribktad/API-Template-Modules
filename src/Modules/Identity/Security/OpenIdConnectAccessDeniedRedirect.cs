using Identity.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Security;

/// <summary>
///     Redirects the browser after OIDC sign-in when <see cref="TenantClaimValidator" /> fails with
///     <see cref="UserAccessDeniedException" /> (invitation / local user gate).
/// </summary>
internal static class OpenIdConnectAccessDeniedRedirect
{
    public static Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        if (context.Exception is not UserAccessDeniedException denied)
            return Task.CompletedTask;

        HttpContext http = context.HttpContext;
        BffOptions bff = http.RequestServices.GetRequiredService<IOptions<BffOptions>>().Value;
        string target = !string.IsNullOrWhiteSpace(bff.AccessDeniedRedirectUri)
            ? bff.AccessDeniedRedirectUri
            : bff.PostLogoutRedirectUri;

        Dictionary<string, string?> query = new()
        {
            ["error"] = "access_denied",
            ["error_code"] = denied.ErrorCode,
            ["error_description"] = denied.Message,
        };

        string url = QueryHelpers.AddQueryString(target, query);
        http.Response.Redirect(url);
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
