using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.Options;

/// <summary>
///     Aggregates per-job configuration options for all registered background jobs in the application.
/// </summary>
public sealed class BackgroundJobsOptions
{
    [Description("Scheduler-level options controlling the TickerQ runtime.")]
    [Required]
    [ValidateObjectMembers]
    public TickerQSchedulerOptions TickerQ { get; set; } = new();

    [Description("Configuration for the recurring external synchronization job.")]
    [Required]
    [ValidateObjectMembers]
    public ExternalSyncJobOptions ExternalSync { get; set; } = new();

    [Description("Configuration for the recurring cleanup job.")]
    [Required]
    [ValidateObjectMembers]
    public CleanupJobOptions Cleanup { get; set; } = new();

    [Description("Configuration for the recurring search reindex job.")]
    [Required]
    [ValidateObjectMembers]
    public ReindexJobOptions Reindex { get; set; } = new();

    [Description("Configuration for the recurring failed-email retry job.")]
    [Required]
    [ValidateObjectMembers]
    public EmailRetryJobOptions EmailRetry { get; set; } = new();

    [Description("Configuration for the recurring orphan-blob reaper job.")]
    [Required]
    [ValidateObjectMembers]
    public OrphanBlobJobOptions OrphanBlob { get; set; } = new();
}
