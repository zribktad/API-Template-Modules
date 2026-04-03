using Microsoft.Extensions.Logging;

namespace ProductCatalog.Logging;

/// <summary>
/// Source-generated logger extension methods for ProductCatalog infrastructure diagnostics.
/// </summary>
internal static partial class ProductCatalogInfrastructureLogs
{
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

