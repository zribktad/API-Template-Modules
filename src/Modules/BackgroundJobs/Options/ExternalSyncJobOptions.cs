using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BackgroundJobs.Options;

/// <summary>
///     Configuration for the scheduled job that synchronises data from external third-party systems.
/// </summary>
public sealed class ExternalSyncJobOptions
{
    [Description("Enables execution of the external synchronization recurring job.")]
    public bool Enabled { get; set; }

    [Description("Five-part CRON expression that schedules the external synchronization job.")]
    [Required]
    [MinLength(1)]
    public string Cron { get; set; } = "0 */12 * * *";
}
