using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace ProductCatalog.Logging;

/// <summary>
/// Source-generated logger extension methods for ProductCatalog application diagnostics.
/// </summary>
internal static partial class ProductCatalogApplicationLogs
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
}

