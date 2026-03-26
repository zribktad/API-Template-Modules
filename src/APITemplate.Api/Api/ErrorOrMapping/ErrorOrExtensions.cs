using APITemplate.Api.Controllers;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.ErrorOrMapping;

/// <summary>
/// Extension methods that convert <see cref="ErrorOr{T}"/> results to <see cref="ActionResult"/>
/// responses, producing the same RFC 7807 ProblemDetails format as <see cref="ExceptionHandling.ApiExceptionHandler"/>.
/// </summary>
public static class ErrorOrExtensions
{
    /// <summary>Maps a successful result to 200 OK, or errors to ProblemDetails.</summary>
    public static ActionResult<T> ToActionResult<T>(
        this ErrorOr<T> result,
        ControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.Ok(result.Value);

        return ToProblemResult<T>(result.Errors, controller);
    }

    /// <summary>Maps a successful result to 201 Created, or errors to ProblemDetails.</summary>
    public static ActionResult<T> ToCreatedResult<T>(
        this ErrorOr<T> result,
        ApiControllerBase controller,
        Func<T, object> routeValuesFactory
    )
    {
        if (!result.IsError)
            return controller.CreatedAtAction(
                "GetById",
                routeValuesFactory(result.Value),
                result.Value
            );

        return ToProblemResult<T>(result.Errors, controller);
    }

    /// <summary>Maps a successful void result to 204 NoContent, or errors to ProblemDetails.</summary>
    public static IActionResult ToNoContentResult(
        this ErrorOr<Success> result,
        ControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.NoContent();

        return ToProblemDetails(result.Errors, controller);
    }

    /// <summary>
    /// Maps a successful batch result through <see cref="ApiControllerBase.OkOrUnprocessable"/>,
    /// or request-level errors to ProblemDetails.
    /// </summary>
    public static ActionResult<BatchResponse> ToBatchResult(
        this ErrorOr<BatchResponse> result,
        ApiControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.OkOrUnprocessable(result.Value);

        return ToProblemResult<BatchResponse>(result.Errors, controller);
    }

    /// <summary>Maps a successful void result to 200 OK, or errors to ProblemDetails.</summary>
    public static IActionResult ToOkResult(this ErrorOr<Success> result, ControllerBase controller)
    {
        if (!result.IsError)
            return controller.Ok();

        return ToProblemDetails(result.Errors, controller);
    }

    /// <summary>
    /// Returns ProblemDetails for the error case of any <see cref="ErrorOr{T}"/> result.
    /// Use when the success case is handled separately by the caller.
    /// </summary>
    public static IActionResult ToErrorResult<T>(this ErrorOr<T> result, ControllerBase controller)
    {
        return ToProblemDetails(result.Errors, controller);
    }

    private static ActionResult<T> ToProblemResult<T>(
        List<ErrorOr.Error> errors,
        ControllerBase controller
    ) => ToProblemDetails(errors, controller);

    private static ObjectResult ToProblemDetails(
        List<ErrorOr.Error> errors,
        ControllerBase controller
    )
    {
        var problemDetails = BuildProblemDetails(errors, controller);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }

    private static ProblemDetails BuildProblemDetails(
        List<ErrorOr.Error> errors,
        ControllerBase controller
    )
    {
        var firstError = errors[0];
        var statusCode = MapToStatusCode(firstError.Type);
        var title = MapToTitle(firstError.Type);
        var errorCode = firstError.Code;
        var detail = firstError.Description;

        if (errors.Count > 1 && firstError.Type == ErrorType.Validation)
            detail = string.Join(" ", errors.Select(e => e.Description));

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = controller.HttpContext.Request.Path,
            Type = BuildTypeUri(errorCode),
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        if (firstError.Metadata is { Count: > 0 })
            problemDetails.Extensions["metadata"] = firstError.Metadata;

        return problemDetails;
    }

    private static int MapToStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

    private static string MapToTitle(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => "Bad Request",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            _ => "Internal Server Error",
        };

    private static string BuildTypeUri(string errorCode) =>
        $"https://api-template.local/errors/{errorCode}";
}
