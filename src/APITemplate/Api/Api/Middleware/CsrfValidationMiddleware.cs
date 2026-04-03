using Identity.Common.Security;
using Microsoft.AspNetCore.Authentication;

namespace APITemplate.Api.Middleware;

/// <summary>
/// Middleware that enforces CSRF protection for cookie-authenticated requests.
/// </summary>
public sealed class CsrfValidationMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (
            HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method)
        )
        {
            await next(context);
            return;
        }

        if (
            context.Request.Headers.TryGetValue(
                Microsoft.Net.Http.Headers.HeaderNames.Authorization,
                out Microsoft.Extensions.Primitives.StringValues authorizationValues
            )
            && authorizationValues.Any(static value =>
                !string.IsNullOrEmpty(value)
                && value.StartsWith(
                    $"{Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme} ",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            await next(context);
            return;
        }

        bool isCookieAuthenticated = context.User.Identities.Any(i =>
            i.AuthenticationType == AuthConstants.BffSchemes.Cookie
        );

        if (!isCookieAuthenticated)
        {
            AuthenticateResult cookieAuthResult = await context.AuthenticateAsync(
                AuthConstants.BffSchemes.Cookie
            );
            isCookieAuthenticated = cookieAuthResult.Succeeded;
        }

        if (!isCookieAuthenticated)
        {
            await next(context);
            return;
        }

        if (
            context.Request.Headers.TryGetValue(
                AuthConstants.Csrf.HeaderName,
                out Microsoft.Extensions.Primitives.StringValues value
            )
            && value.Any(v =>
                string.Equals(v, AuthConstants.Csrf.HeaderValue, StringComparison.Ordinal)
            )
        )
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail =
                        $"Cookie-authenticated requests must include the '{AuthConstants.Csrf.HeaderName}: {AuthConstants.Csrf.HeaderValue}' header.",
                },
            }
        );
    }
}
