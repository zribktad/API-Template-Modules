using System.Data;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Compile-time source-generated logger extension methods for <see cref="UnitOfWork"/> diagnostics.
/// </summary>
internal static partial class UnitOfWorkLogs
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "CommitAsync was called inside ExecuteInTransactionAsync and was rejected."
    )]
    public static partial void CommitRejectedInsideManagedTransaction(this ILogger logger);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "CommitAsync started. RetryEnabled={RetryEnabled}, TimeoutSeconds={TimeoutSeconds}"
    )]
    public static partial void CommitStarted(
        this ILogger logger,
        bool retryEnabled,
        int? timeoutSeconds
    );

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Debug,
        Message = "CommitAsync completed successfully."
    )]
    public static partial void CommitCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Debug,
        Message = "Outermost transaction started. IsolationLevel={IsolationLevel}, TimeoutSeconds={TimeoutSeconds}, RetryEnabled={RetryEnabled}"
    )]
    public static partial void OutermostTransactionStarted(
        this ILogger logger,
        IsolationLevel isolationLevel,
        int? timeoutSeconds,
        bool retryEnabled
    );

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Debug,
        Message = "Explicit database transaction opened."
    )]
    public static partial void DatabaseTransactionOpened(this ILogger logger);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Warning,
        Message = "Provider does not support explicit database transactions. Continuing without an explicit transaction."
    )]
    public static partial void DatabaseTransactionUnsupported(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Debug,
        Message = "Explicit database transaction committed."
    )]
    public static partial void DatabaseTransactionCommitted(this ILogger logger);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Warning,
        Message = "Explicit database transaction rolled back due to an exception."
    )]
    public static partial void DatabaseTransactionRolledBack(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = 5009,
        Level = LogLevel.Debug,
        Message = "Outermost transaction completed successfully."
    )]
    public static partial void OutermostTransactionCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Debug,
        Message = "Creating savepoint {SavepointName}."
    )]
    public static partial void SavepointCreating(this ILogger logger, string savepointName);

    [LoggerMessage(
        EventId = 5011,
        Level = LogLevel.Debug,
        Message = "Released savepoint {SavepointName}."
    )]
    public static partial void SavepointReleased(this ILogger logger, string savepointName);

    [LoggerMessage(
        EventId = 5012,
        Level = LogLevel.Debug,
        Message = "Rolled back to savepoint {SavepointName}."
    )]
    public static partial void SavepointRolledBack(this ILogger logger, string savepointName);
}
