namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Application-layer contract for scheduled data-cleanup operations.
/// Implementations live in the Infrastructure layer and are invoked by recurring background jobs.
/// </summary>
public interface ICleanupService
{
    /// <summary>
    /// Removes expired tenant invitations older than <paramref name="retentionHours"/> hours,
    /// processed in batches of <paramref name="batchSize"/> to limit database pressure.
    /// </summary>
    Task CleanupExpiredInvitationsAsync(
        int retentionHours,
        int batchSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Permanently purges soft-deleted records that exceeded the <paramref name="retentionDays"/> retention window,
    /// processed in batches of <paramref name="batchSize"/>.
    /// </summary>
    Task CleanupSoftDeletedRecordsAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Deletes product-data entries that are no longer referenced by any product and have exceeded
    /// the <paramref name="retentionDays"/> retention window, processed in batches of <paramref name="batchSize"/>.
    /// </summary>
    Task CleanupOrphanedProductDataAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    );
}
