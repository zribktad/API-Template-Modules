using ErrorOr;
using Microsoft.AspNetCore.Http;
using SharedKernel.Application.Errors;

namespace SharedKernel.Contracts.Api;

public static class ErrorOrHttpResultExtensions
{
    public static IResult ToHttpResult<T>(this ErrorOr<T> result, HttpContext httpContext)
        => result.IsError
            ? Results.Problem(result.Errors.ToProblemDetails(httpContext))
            : TypedResults.Ok(result.Value);

    public static IResult ToHttpCreatedResult<T>(this ErrorOr<T> result, HttpContext httpContext, string location)
        => result.IsError
            ? Results.Problem(result.Errors.ToProblemDetails(httpContext))
            : TypedResults.Created(location, result.Value);

    public static IResult ToHttpNoContentResult(this ErrorOr<Success> result, HttpContext httpContext)
        => result.IsError
            ? Results.Problem(result.Errors.ToProblemDetails(httpContext))
            : TypedResults.NoContent();
}
