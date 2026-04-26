using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.ExceptionHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequestStatusCode = 499;
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly ApiExceptionMetrics _metrics;
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(
        ILogger<ApiExceptionHandler> logger,
        IProblemDetailsService problemDetailsService,
        ApiExceptionMetrics metrics
    )
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _metrics = metrics;
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

        (
            int statusCode,
            string title,
            string detail,
            string errorCode,
            IReadOnlyDictionary<string, object>? metadata
        ) = Resolve(exception);
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["errorCode"] = errorCode;

        if (metadata is { Count: > 0 })
            problemDetails.Extensions["metadata"] = metadata;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.UnhandledException(exception, statusCode, errorCode, context.TraceIdentifier);
            _metrics.RecordUnhandledException(statusCode, errorCode);
        }
        else
        {
            _logger.MappedInfrastructureException(
                exception,
                statusCode,
                errorCode,
                context.TraceIdentifier
            );
            _metrics.RecordMappedInfrastructureException(statusCode, errorCode);
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

    private static (
        int StatusCode,
        string Title,
        string Detail,
        string ErrorCode,
        IReadOnlyDictionary<string, object>? Metadata
    ) Resolve(Exception exception)
    {
        if (ApiExceptionMapper.TryMap(exception, out var mapped))
        {
            return mapped;
        }

        IReadOnlyDictionary<string, object>? metadata = exception is IHasErrorMetadata hasMetadata
            ? hasMetadata.Metadata
            : null;

        string errorCode = exception is IHasErrorCode hasErrorCode
            ? hasErrorCode.ErrorCode
            : ErrorCatalog.General.Unknown;

        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            errorCode,
            metadata
        );
    }
}
