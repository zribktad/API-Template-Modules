using APITemplate.Domain.Exceptions;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.ExceptionHandling;

/// <summary>
/// Central REST exception translator for the HTTP pipeline.
/// </summary>
/// <remarks>
/// Converts domain/application exceptions to RFC7807 <see cref="ProblemDetails"/>
/// responses with stable status codes and error codes. GraphQL requests are intentionally
/// bypassed because GraphQL uses a separate error handling pipeline.
/// </remarks>
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

    /// <summary>
    /// Maps an exception to HTTP status + payload metadata, logs it with severity by status code,
    /// and writes an RFC7807 response through <see cref="IProblemDetailsService"/>.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // GraphQL has its own error format and middleware, so let that pipeline handle GraphQL exceptions.
        if (context.Request.Path.StartsWithSegments(TelemetryPathPrefixes.GraphQl))
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
            Type = BuildTypeUri(errorCode),
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        if (metadata is not null && metadata.Count > 0)
            problemDetails.Extensions["metadata"] = metadata;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.UnhandledException(exception, statusCode, errorCode, context.TraceIdentifier);
        }
        else
        {
            _logger.HandledApplicationException(
                exception,
                statusCode,
                errorCode,
                context.TraceIdentifier
            );
        }

        ApiMetrics.RecordHandledException(statusCode, errorCode, exception.GetType().Name);

        ConflictTelemetry.Record(exception, errorCode);

        context.Response.StatusCode = statusCode;
        var wasWritten = await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = problemDetails,
            }
        );

        return wasWritten;
    }

    private static bool IsClientAbortedRequest(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    ) =>
        exception is OperationCanceledException
        && (
            context.RequestAborted.IsCancellationRequested
            || cancellationToken.IsCancellationRequested
        );

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
            var errorCode = ResolveErrorCode(appException, defaultErrorCode);

            return (statusCode, title, appException.Message, errorCode, appException.Metadata);
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

    private static (int StatusCode, string Title, string ErrorCode) MapToHttp(
        AppException appException
    ) =>
        appException switch
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

    private static string ResolveErrorCode(AppException appException, string defaultErrorCode)
    {
        if (!string.IsNullOrWhiteSpace(appException.ErrorCode))
            return appException.ErrorCode!;

        if (
            appException.Metadata is not null
            && appException.Metadata.TryGetValue("errorCode", out var metadataErrorCode)
            && metadataErrorCode is string value
            && !string.IsNullOrWhiteSpace(value)
        )
        {
            return value;
        }

        return defaultErrorCode;
    }

    private static string BuildTypeUri(string errorCode) =>
        $"https://api-template.local/errors/{errorCode}";
}
