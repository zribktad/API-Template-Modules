using SharedKernel.Infrastructure.Logging;

namespace APITemplate.Api.Cache;

/// <summary>
/// Source-generated logging contract for <see cref="OutputCacheInvalidationService"/>.
/// Keeps log templates and event identifiers centralized, strongly typed, and allocation-friendly.
/// </summary>
internal static partial class OutputCacheInvalidationServiceLogs
{
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to evict output cache for tag: {Tag}"
    )]
    public static partial void EvictOutputCacheFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string tag
    );
}
