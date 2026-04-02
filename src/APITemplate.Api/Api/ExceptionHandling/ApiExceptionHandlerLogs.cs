using SharedKernel.Infrastructure.Logging;

namespace APITemplate.Api.ExceptionHandling;

/// <summary>
/// Source-generated logging contract for <see cref="ApiExceptionHandler"/>.
/// Keeps log templates and event identifiers centralized, strongly typed, and allocation-friendly.
/// </summary>
internal static partial class ApiExceptionHandlerLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}"
    )]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        [SensitiveData] string errorCode,
        [PersonalData] string traceId
    );

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Handled application exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}"
    )]
    public static partial void HandledApplicationException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        [SensitiveData] string errorCode,
        [PersonalData] string traceId
    );
}
