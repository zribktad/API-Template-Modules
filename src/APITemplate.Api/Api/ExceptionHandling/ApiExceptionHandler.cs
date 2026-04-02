using SharedKernel.Domain.Exceptions;
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

        var (statusCode, title, detail, errorCode, metadata) = Resolve(exception);
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://api-template.local/errors/{errorCode}",
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        if (metadata is not null && metadata.Count > 0)
            problemDetails.Extensions["metadata"] = metadata;

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for trace {TraceId}.", context.TraceIdentifier);
        else
            _logger.LogWarning(exception, "Handled exception for trace {TraceId}.", context.TraceIdentifier);

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
    ) =>
        exception is OperationCanceledException
        && (context.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested);

    private static (
        int StatusCode,
        string Title,
        string Detail,
        string ErrorCode,
        IReadOnlyDictionary<string, object?>? Metadata
    ) Resolve(Exception exception)
    {
        if (exception is AppException appException)
        {
            var (statusCode, title, defaultErrorCode) = MapToHttp(appException);
            var errorCode = string.IsNullOrWhiteSpace(appException.ErrorCode)
                ? defaultErrorCode
                : appException.ErrorCode;

            return (statusCode, title, appException.Message, errorCode!, appException.Metadata);
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request. Please retrieve the latest version and retry.",
                ErrorCatalog.General.ConcurrencyConflict,
                null
            );
        }

        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            ErrorCatalog.General.Unknown,
            null
        );
    }

    private static (int StatusCode, string Title, string ErrorCode) MapToHttp(AppException exception) =>
        exception switch
        {
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                ErrorCatalog.General.ValidationFailed
            ),
            UnauthorizedException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                ErrorCatalog.General.Unknown
            ),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                ErrorCatalog.Auth.Forbidden
            ),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                ErrorCatalog.General.NotFound
            ),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ErrorCatalog.General.Conflict
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                ErrorCatalog.General.Unknown
            ),
        };
}
