using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.BackgroundJobs;

/// <summary>
/// Configuration for the periodic cleanup job that purges expired invitations, soft-deleted records,
/// and orphaned product data according to the configured retention windows.
/// </summary>
public sealed class CleanupJobOptions
{
    [Description("Enables execution of the cleanup recurring job.")]
    public bool Enabled { get; set; }

    [Description("Five-part CRON expression that schedules the cleanup job.")]
    [Required]
    [MinLength(1)]
    public string Cron { get; set; } = "0 * * * *";

    [Description("Retention window, in hours, before expired invitations are deleted.")]
    [Range(0, int.MaxValue)]
    public int ExpiredInvitationRetentionHours { get; set; } = 168;

    [Description("Retention window, in days, before soft-deleted records are purged.")]
    [Range(0, int.MaxValue)]
    public int SoftDeleteRetentionDays { get; set; } = 30;

    [Description("Retention window, in days, before orphaned product data is removed.")]
    [Range(0, int.MaxValue)]
    public int OrphanedProductDataRetentionDays { get; set; } = 7;

    [Description("Maximum number of records processed in a single cleanup batch.")]
    [Range(1, int.MaxValue)]
    public int BatchSize { get; set; } = 100;
}
