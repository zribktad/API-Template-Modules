using Microsoft.Extensions.Logging;

namespace ProductCatalog.Logging;

/// <summary>
///     Source-generated logger extension methods for ProductCatalog diagnostics.
/// </summary>
internal static partial class ProductCatalogLogs
{
    // ProductDataCascadeDeleteHandler (4001-4002)
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Cascade soft-deleted {Count} ProductData documents for tenant {TenantId}."
    )]
    public static partial void ProductDataCascadeDeleteSucceeded(
        this ILogger logger,
        long count,
        Guid tenantId
    );

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Failed to cascade soft-delete ProductData documents for tenant {TenantId}. EF entities are already soft-deleted."
    )]
    public static partial void ProductDataCascadeDeleteFailed(
        this ILogger logger,
        Exception exception,
        Guid tenantId
    );

    // DeleteProductDataCommandHandler (4010)
    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Error,
        Message = "Failed to soft-delete ProductData document {ProductDataId} for tenant {TenantId}. Related ProductDataLinks may already be soft-deleted in PostgreSQL."
    )]
    public static partial void ProductDataSoftDeleteFailed(
        this ILogger logger,
        Exception exception,
        Guid productDataId,
        Guid tenantId
    );

    // CleanupOrphanedProductDataHandler (4050)
    [LoggerMessage(
        EventId = 4050,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} orphaned product data documents."
    )]
    public static partial void OrphanedProductDataCleanedUp(this ILogger logger, int count);

    // MongoDbHealthCheck (4051)
    [LoggerMessage(EventId = 4051, Level = LogLevel.Error, Message = "MongoDB health check failed")]
    public static partial void MongoDbHealthCheckFailed(this ILogger logger, Exception exception);
}
