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
        if (context.Request.Path.StartsWithSegments(GraphQLPathConstants.BasePath))
            return false;

        if (IsClientAbortedRequest(context, exception, cancellationToken))
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = ClientClosedRequestStatusCode;

            return true;
        }

        MappedApiError resolved = Resolve(exception);
        ProblemDetails problemDetails = new()
        {
            Status = resolved.StatusCode,
            Title = resolved.Title,
            Detail = resolved.Detail,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["errorCode"] = resolved.ErrorCode;

        // Only surface error metadata for client errors (<500). For server errors the metadata may
        // carry internal details (Keycloak/SMTP/DB messages) that must not leak to callers; it is
        // still logged below.
        if (
            resolved.Metadata is { Count: > 0 }
            && resolved.StatusCode < StatusCodes.Status500InternalServerError
        )
            problemDetails.Extensions["metadata"] = resolved.Metadata;

        if (resolved.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.UnhandledException(
                exception,
                resolved.StatusCode,
                resolved.ErrorCode,
                context.TraceIdentifier
            );
            _metrics.RecordUnhandledException(resolved.StatusCode, resolved.ErrorCode);
        }
        else
        {
            _logger.MappedInfrastructureException(
                exception,
                resolved.StatusCode,
                resolved.ErrorCode,
                context.TraceIdentifier
            );
            _metrics.RecordMappedInfrastructureException(resolved.StatusCode, resolved.ErrorCode);
        }

        context.Response.StatusCode = resolved.StatusCode;

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

    private static MappedApiError Resolve(Exception exception)
    {
        if (ApiExceptionMapper.TryMap(exception, out MappedApiError mapped))
            return mapped;

        IReadOnlyDictionary<string, object>? metadata = exception is IHasErrorMetadata hasMetadata
            ? hasMetadata.Metadata
            : null;

        string errorCode = exception is IHasErrorCode hasErrorCode
            ? hasErrorCode.ErrorCode
            : ErrorCatalog.General.Unknown;

        return new MappedApiError(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.",
            errorCode,
            metadata
        );
    }
}
