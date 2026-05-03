using BuildingBlocks.Web.Logging;
using Microsoft.Extensions.Logging;

namespace APITemplate.Api;

/// <summary>
///     Source-generated logger extension methods for API diagnostics.
/// </summary>
internal static partial class ApiLogs
{
    // ApiExceptionHandler (1001, 1003)
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}"
    )]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        string errorCode,
        string traceId
    );

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Mapped infrastructure exception to API error. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}"
    )]
    public static partial void MappedInfrastructureException(
        this ILogger logger,
        Exception exception,
        int statusCode,
        string errorCode,
        string traceId
    );

    // OutputCacheInvalidationService (1005)
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "Failed to evict output cache for tag: {Tag}"
    )]
    public static partial void EvictOutputCacheFailed(
        this ILogger logger,
        Exception exception,
        string tag
    );

    // Infrastructure Diagnostics (1100-1101)
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Distributed Rate Limiting is active using Redis backplane."
    )]
    public static partial void DistributedRateLimitingActive(this ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Rate Limiting is running in local In-Memory mode (Distributed disabled or Redis missing)."
    )]
    public static partial void InMemoryRateLimitingActive(this ILogger logger);
}
