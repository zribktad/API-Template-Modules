using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Application.DTOs;

namespace SharedKernel.Contracts.Api;

public static class ErrorOrExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ErrorOr<T> result, ControllerBase controller)
    {
        if (!result.IsError)
            return controller.Ok(result.Value);

        return ToProblemResult<T>(result.Errors, controller);
    }

    public static ActionResult<T> ToCreatedResult<T>(
        this ErrorOr<T> result,
        ApiControllerBase controller,
        Func<T, object> routeValuesFactory
    )
    {
        if (!result.IsError)
            return controller.CreatedAtAction("GetById", routeValuesFactory(result.Value), result.Value);

        return ToProblemResult<T>(result.Errors, controller);
    }

    public static IActionResult ToNoContentResult(this ErrorOr<Success> result, ControllerBase controller)
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
    /// Returns ProblemDetails for the error case of any <see cref="ErrorOr{T}"/> result.
    /// Use when the success case is handled separately by the caller.
    /// </summary>
    public static IActionResult ToErrorResult<T>(this ErrorOr<T> result, ControllerBase controller) =>
        ToProblemDetails(result.Errors, controller);

    public static ActionResult<BatchResponse> ToBatchResult(
        this ErrorOr<BatchResponse> result,
        ApiControllerBase controller
    )
    {
        if (!result.IsError)
            return controller.OkOrUnprocessable(result.Value);

        return ToProblemResult<BatchResponse>(result.Errors, controller);
    }

    private static ActionResult<T> ToProblemResult<T>(List<ErrorOr.Error> errors, ControllerBase controller) =>
        ToProblemDetails(errors, controller);

    private static ObjectResult ToProblemDetails(List<ErrorOr.Error> errors, ControllerBase controller)
    {
        ErrorOr.Error firstError = errors[0];
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

        string detail = errors.Count > 1 && firstError.Type == ErrorType.Validation
            ? string.Join(" ", errors.Select(e => e.Description))
            : firstError.Description;

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = controller.HttpContext.Request.Path,
            Type = $"{controller.HttpContext.Request.Scheme}://{controller.HttpContext.Request.Host}/errors/{firstError.Code}",
        };

        problemDetails.Extensions["errorCode"] = firstError.Code;
        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        if (firstError.Metadata is { Count: > 0 })
            problemDetails.Extensions["metadata"] = firstError.Metadata;

        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
