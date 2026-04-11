using ErrorOr;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Contracts.Api;

namespace Identity.Security;

/// <summary>
///     When token validation fails with <see cref="UserAccessDeniedException" />, writes RFC 7807
///     ProblemDetails instead of an empty 401 (so API clients can read <c>errorCode</c> / detail).
/// </summary>
internal static class JwtBearerAccessDeniedChallenge
{
    public static async Task OnChallengeAsync(JwtBearerChallengeContext context)
    {
        HttpContext http = context.HttpContext;

        if (
            !http.Items.TryGetValue(
                AuthConstants.HttpContextItems.AccessDeniedErrorCode,
                out object? codeObj
            ) || codeObj is not string errorCode
        )
        {
            return;
        }

        if (
            !http.Items.TryGetValue(
                AuthConstants.HttpContextItems.AccessDeniedDetail,
                out object? detailObj
            ) || detailObj is not string detail
        )
        {
            return;
        }

        context.HandleResponse();

        IProblemDetailsService problemDetails =
            http.RequestServices.GetRequiredService<IProblemDetailsService>();

        ProblemDetails problem = Error.Unauthorized(errorCode, detail).ToProblemDetails(http);

        await problemDetails.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }
        );
    }
}
