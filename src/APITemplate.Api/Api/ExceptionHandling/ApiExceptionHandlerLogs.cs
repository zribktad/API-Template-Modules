using APITemplate.Infrastructure.Logging;

namespace APITemplate.Api.ExceptionHandling;

/// <summary>
/// Source-generated logging contract for <see cref="ApiExceptionHandler"/>.
/// Keeps log templates and event identifiers centralized, strongly typed, and allocation-friendly.
/// </summary>
internal static partial class ApiExceptionHandlerLogs
{
    /// <summary>
    /// Logs an unhandled server-side exception (typically HTTP 5xx).
    /// </summary>
    /// <param name="logger">Target logger instance.</param>
    /// <param name="exception">Captured exception to attach to the log event.</param>
    /// <param name="statusCode">HTTP status code returned to the client.</param>
    /// <param name="errorCode">Application error code. Classified as sensitive for redaction.</param>
    /// <param name="traceId">Request trace identifier. Classified as personal for redaction.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}")]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        [SensitiveData] string errorCode,
        [PersonalData] string traceId);

    /// <summary>
    /// Logs a handled application exception (typically HTTP 4xx).
    /// </summary>
    /// <param name="logger">Target logger instance.</param>
    /// <param name="exception">Captured exception to attach to the log event.</param>
    /// <param name="statusCode">HTTP status code returned to the client.</param>
    /// <param name="errorCode">Application error code. Classified as sensitive for redaction.</param>
    /// <param name="traceId">Request trace identifier. Classified as personal for redaction.</param>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Handled application exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}")]
    public static partial void HandledApplicationException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        [SensitiveData] string errorCode,
        [PersonalData] string traceId);
}
