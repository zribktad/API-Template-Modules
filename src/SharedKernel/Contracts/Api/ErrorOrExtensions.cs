using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.DTOs;

namespace SharedKernel.Contracts.Api;

public static class ErrorOrExtensions
{
    public static ActionResult<T> ToActionResult<T>(
        this ErrorOr<T> result,
        ControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.Ok(result.Value);

        return ToProblemResult<T>(result.Errors, controller);
    }

    public static ActionResult<T> ToCreatedResult<T>(
        this ErrorOr<T> result,
        ApiControllerBase controller,
        string actionName,
        Func<T, object> routeValuesFactory
    )
    {
        if (!result.IsError)
        {
            return controller.CreatedAtAction(
                actionName,
                routeValuesFactory(result.Value),
                result.Value
            );
        }

        return ToProblemResult<T>(result.Errors, controller);
    }

    public static IActionResult ToNoContentResult(
        this ErrorOr<Success> result,
        ControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.NoContent();

        return ToProblemDetails(result.Errors, controller);
    }

    public static IActionResult ToOkResult(this ErrorOr<Success> result, ControllerBase controller)
    {
        if (!result.IsError)
            return controller.Ok();

        return ToProblemDetails(result.Errors, controller);
    }

    /// <summary>
    ///     Returns ProblemDetails for the error case of any <see cref="ErrorOr{T}" /> result.
    ///     Use when the success case is handled separately by the caller.
    /// </summary>
    public static IActionResult ToErrorResult<T>(this ErrorOr<T> result, ControllerBase controller)
    {
        return ToProblemDetails(result.Errors, controller);
    }

    public static ActionResult<BatchResponse> ToBatchResult(
        this ErrorOr<BatchResponse> result,
        ApiControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.OkOrUnprocessable(result.Value);

        return ToProblemResult<BatchResponse>(result.Errors, controller);
    }

    private static ActionResult<T> ToProblemResult<T>(List<Error> errors, ControllerBase controller)
    {
        return ToProblemDetails(errors, controller);
    }

    /// <summary>
    ///     Builds RFC 7807 <see cref="ProblemDetails" /> from one or more <see cref="Error" /> values using the same
    ///     rules as controller <see cref="ToActionResult{T}" /> — for middleware and authentication handlers that are not
    ///     MVC actions.
    /// </summary>
    public static ProblemDetails ToProblemDetails(this Error error, HttpContext httpContext) =>
        new[] { error }.ToProblemDetails(httpContext);

    /// <inheritdoc cref="ToProblemDetails(Error, HttpContext)" />
    public static ProblemDetails ToProblemDetails(
        this IReadOnlyList<Error> errors,
        HttpContext httpContext
    )
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            errors =
            [
                Error.Unexpected(
                    SharedKernel.Application.Errors.ErrorCatalog.General.Unknown,
                    "An unexpected error occurred."
                ),
            ];

        Error firstError = errors[0];
        int statusCode = firstError.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        string title = firstError.Type switch
        {
            ErrorType.Validation => "Bad Request",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            _ => "Internal Server Error",
        };

        string detail =
            errors.Count > 1 && firstError.Type == ErrorType.Validation
                ? string.Join(" ", errors.Select(e => e.Description))
                : firstError.Description;

        string errorCode = firstError.Code;
        string? configuredType = httpContext.RequestServices.GetService<
            IOptions<ErrorDocumentationOptions>
        >()
            is { } docOpts
            ? ProblemDetailsErrorTypeUri.BuildAbsoluteUri(docOpts.Value.ErrorTypeBaseUri, errorCode)
            : null;

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type =
                configuredType
                ?? ProblemDetailsErrorTypeUri.BuildFallbackUri(
                    httpContext.Request.Scheme,
                    httpContext.Request.Host.ToString(),
                    errorCode
                ),
        };

        problemDetails.Extensions[ProblemDetailsConstants.ErrorCode] = firstError.Code;
        problemDetails.Extensions[ProblemDetailsConstants.TraceId] = httpContext.TraceIdentifier;

        if (firstError.Metadata is { Count: > 0 })
            problemDetails.Extensions[ProblemDetailsConstants.Metadata] = firstError.Metadata;

        return problemDetails;
    }

    private static ObjectResult ToProblemDetails(List<Error> errors, ControllerBase controller)
    {
        ProblemDetails problemDetails = errors.ToProblemDetails(controller.HttpContext);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
