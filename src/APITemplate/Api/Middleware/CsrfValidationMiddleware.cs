using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Middleware that enforces CSRF protection for cookie-authenticated requests.
/// </summary>
public sealed class CsrfValidationMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService,
    IBffCsrfTokenService csrfTokens
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSafeMethodExcludingBffLogoutGet(context))
        {
            await next(context);
            return;
        }

        if (HasBearerAuthorization(context))
        {
            await next(context);
            return;
        }

        AuthenticateResult cookieAuth = await context.AuthenticateAsync(
            AuthConstants.BffSchemes.Cookie
        );
        if (!cookieAuth.Succeeded)
        {
            await next(context);
            return;
        }

        if (!cookieAuth.Properties.TryGetBffSessionId(out string? sessionId))
        {
            await WriteCsrfForbiddenAsync(
                context,
                $"Cookie session is missing a BFF session id; cannot validate '{AuthConstants.Csrf.HeaderName}'."
            );
            return;
        }

        if (
            !context.Request.Headers.TryGetValue(
                AuthConstants.Csrf.HeaderName,
                out StringValues values
            )
        )
        {
            await WriteCsrfForbiddenAsync(
                context,
                $"Cookie-authenticated requests must include a valid '{AuthConstants.Csrf.HeaderName}' header (see GET /api/v1/bff/csrf)."
            );
            return;
        }

        bool anyNonEmpty = false;
        bool anyValid = false;
        foreach (string? v in values)
        {
            if (string.IsNullOrEmpty(v))
                continue;
            anyNonEmpty = true;
            if (csrfTokens.IsValid(sessionId, v))
            {
                anyValid = true;
                break;
            }
        }

        if (!anyNonEmpty)
        {
            await WriteCsrfForbiddenAsync(
                context,
                $"Cookie-authenticated requests must include a valid '{AuthConstants.Csrf.HeaderName}' header (see GET /api/v1/bff/csrf)."
            );
            return;
        }

        if (!anyValid)
        {
            await WriteCsrfForbiddenAsync(
                context,
                $"Invalid or expired '{AuthConstants.Csrf.HeaderName}' header for this session."
            );
            return;
        }

        await next(context);
    }

    private static bool IsSafeMethodExcludingBffLogoutGet(HttpContext context)
    {
        bool isBffLogoutGet =
            HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path.Equals(
                AuthConstants.BffRoutes.Logout,
                StringComparison.OrdinalIgnoreCase
            );

        return (HttpMethods.IsGet(context.Request.Method) && !isBffLogoutGet)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method);
    }

    private static bool HasBearerAuthorization(HttpContext context)
    {
        return context.Request.Headers.TryGetValue(
                HeaderNames.Authorization,
                out StringValues authorizationValues
            )
            && authorizationValues.Any(static value =>
                !string.IsNullOrEmpty(value)
                && value.StartsWith(
                    $"{JwtBearerDefaults.AuthenticationScheme} ",
                    StringComparison.OrdinalIgnoreCase
                )
            );
    }

    private async Task WriteCsrfForbiddenAsync(HttpContext context, string detail)
    {
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
                    Detail = detail,
                },
            }
        );
    }
}
