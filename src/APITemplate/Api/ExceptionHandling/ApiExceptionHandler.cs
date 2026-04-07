using APITemplate.Api;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.ExceptionHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequestStatusCode = 499;
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(
        ILogger<ApiExceptionHandler> logger,
        IProblemDetailsService problemDetailsService
    )
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (context.Request.Path.StartsWithSegments("/graphql"))
            return false;

        if (IsClientAbortedRequest(context, exception, cancellationToken))
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = ClientClosedRequestStatusCode;

            return true;
        }

        (int statusCode, string title, string detail, string errorCode) = Resolve(exception);
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["errorCode"] = errorCode;

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.UnhandledException(exception, statusCode, errorCode, context.TraceIdentifier);
        else
        {
            _logger.HandledApplicationException(
                exception,
                statusCode,
                errorCode,
                context.TraceIdentifier
            );
        }

        context.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = problemDetails,
            }
        );
    }

    private static bool IsClientAbortedRequest(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        return exception is OperationCanceledException
            && (
                context.RequestAborted.IsCancellationRequested
                || cancellationToken.IsCancellationRequested
            );
    }

    private static (int StatusCode, string Title, string Detail, string ErrorCode) Resolve(
        Exception exception
    )
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request. Please retrieve the latest version and retry.",
                ErrorCatalog.General.ConcurrencyConflict
            );
        }

        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            ErrorCatalog.General.Unknown
        );
    }
}
