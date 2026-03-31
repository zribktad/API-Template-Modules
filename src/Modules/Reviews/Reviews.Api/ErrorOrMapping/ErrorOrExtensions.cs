using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reviews.Api.Controllers;

namespace Reviews.Api.ErrorOrMapping;

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

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = errors.Count > 1 && firstError.Type == ErrorType.Validation
                ? string.Join(" ", errors.Select(error => error.Description))
                : firstError.Description,
            Instance = controller.HttpContext.Request.Path,
            Type = $"{controller.HttpContext.Request.Scheme}://{controller.HttpContext.Request.Host}/errors/{firstError.Code}",
        };

        problemDetails.Extensions["errorCode"] = firstError.Code;
        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
