using Microsoft.Extensions.Logging;

namespace BackgroundJobs.Logging;

/// <summary>
///     Source-generated logger extension methods for BackgroundJobs infrastructure diagnostics.
/// </summary>
internal static partial class BackgroundJobsInfrastructureLogs
{
    // CleanupService (8001)
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} soft-deleted records from {Entity}."
    )]
    public static partial void CleanedUpSoftDeletedRecords(
        this ILogger logger,
        int count,
        string entity
    );

    // ExternalIntegrationSyncServicePreview (8002)
    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "External integration synchronization job executed, but no provider-specific synchronization workflow is registered yet."
    )]
    public static partial void ExternalIntegrationSyncNoProviderRegistered(this ILogger logger);

    // JobProcessingBackgroundService (8003, 8004)
    [LoggerMessage(EventId = 8003, Level = LogLevel.Error, Message = "Job {JobId} failed")]
    public static partial void JobFailed(this ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Error,
        Message = "Failed to mark job {JobId} as failed"
    )]
    public static partial void MarkJobFailedError(
        this ILogger logger,
        Exception exception,
        Guid jobId
    );

    // ReindexService (8005, 8006, 8007, 8008)
    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Warning,
        Message = "Skipping invalid FTS index name: {IndexName}."
    )]
    public static partial void SkippingInvalidFtsIndexName(this ILogger logger, string indexName);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Debug,
        Message = "FTS index {IndexName} bloat {BloatPercent:F1}% is below threshold {Threshold}%, skipping."
    )]
    public static partial void FtsIndexBloatBelowThreshold(
        this ILogger logger,
        string indexName,
        double bloatPercent,
        double threshold
    );

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Information,
        Message = "FTS index {IndexName} bloat {BloatPercent:F1}% exceeds threshold {Threshold}%, reindexing."
    )]
    public static partial void FtsIndexBloatExceedsThreshold(
        this ILogger logger,
        string indexName,
        double bloatPercent,
        double threshold
    );

    [LoggerMessage(
        EventId = 8008,
        Level = LogLevel.Information,
        Message = "Reindexed FTS index {IndexName}."
    )]
    public static partial void FtsIndexReindexed(this ILogger logger, string indexName);

    // RedisDistributedJobCoordinator (8009, 8010, 8011, 8012)
    [LoggerMessage(
        EventId = 8009,
        Level = LogLevel.Debug,
        Message = "Skipped background job {JobName} because another instance currently owns the coordination lease."
    )]
    public static partial void JobSkippedLeadershipNotAcquired(this ILogger logger, string jobName);

    [LoggerMessage(
        EventId = 8010,
        Level = LogLevel.Warning,
        Message = "Redis coordination is unavailable for background job {JobName}; continuing because fail-closed is disabled. {Message}"
    )]
    public static partial void CoordinationUnavailableFailOpenContinuing(
        this ILogger logger,
        Exception? exception,
        string jobName,
        string message
    );

    [LoggerMessage(
        EventId = 8011,
        Level = LogLevel.Warning,
        Message = "Fail-closed coordination stopped background job {JobName}: {Message}"
    )]
    public static partial void CoordinationFailClosedStopped(
        this ILogger logger,
        Exception? exception,
        string jobName,
        string message
    );

    [LoggerMessage(
        EventId = 8012,
        Level = LogLevel.Warning,
        Message = "Error while renewing coordination lease for background job {JobName}. Proceeding to release the lock."
    )]
    public static partial void CoordinationLeaseRenewalError(
        this ILogger logger,
        Exception exception,
        string jobName
    );

    [LoggerMessage(
        EventId = 8013,
        Level = LogLevel.Warning,
        Message = "Lost Redis coordination lease for background job {JobName}; cancelling the in-flight execution."
    )]
    public static partial void CoordinationLeaseLost(this ILogger logger, string jobName);

    // TickerQ recurring jobs (8014, 8015, 8016)
    [LoggerMessage(
        EventId = 8014,
        Level = LogLevel.Information,
        Message = "Executing cleanup recurring job for ticker {TickerId}."
    )]
    public static partial void ExecutingCleanupRecurringJob(this ILogger logger, Guid tickerId);

    [LoggerMessage(
        EventId = 8015,
        Level = LogLevel.Information,
        Message = "Executing external integration sync recurring job for ticker {TickerId}."
    )]
    public static partial void ExecutingExternalSyncRecurringJob(
        this ILogger logger,
        Guid tickerId
    );

    [LoggerMessage(
        EventId = 8016,
        Level = LogLevel.Information,
        Message = "Executing reindex recurring job for ticker {TickerId}."
    )]
    public static partial void ExecutingReindexRecurringJob(this ILogger logger, Guid tickerId);

    // TickerQRecurringJobRegistrar (8017)
    [LoggerMessage(
        EventId = 8017,
        Level = LogLevel.Information,
        Message = "Synchronized {Count} recurring TickerQ job definitions."
    )]
    public static partial void TickerQJobDefinitionsSynchronized(this ILogger logger, int count);
}
