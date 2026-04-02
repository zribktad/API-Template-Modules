using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.BackgroundJobs;

/// <summary>
/// Configuration for the background job that retries failed outbound email deliveries
/// and moves messages to the dead-letter queue after the maximum retry threshold is exceeded.
/// </summary>
public sealed class EmailRetryJobOptions
{
    [Description("Enables execution of the failed-email retry recurring job.")]
    public bool Enabled { get; set; }

    [Description("Five-part CRON expression that schedules the failed-email retry job.")]
    [Required]
    [MinLength(1)]
    public string Cron { get; set; } = "*/15 * * * *";

    [Description(
        "Maximum number of delivery attempts before an email is treated as permanently failed."
    )]
    [Range(1, int.MaxValue)]
    public int MaxRetryAttempts { get; set; } = 5;

    [Description("Maximum number of queued failed emails processed in a single retry batch.")]
    [Range(1, int.MaxValue)]
    public int BatchSize { get; set; } = 50;

    [Description(
        "Age threshold, in hours, after which failed emails are moved to dead-letter state."
    )]
    [Range(0, int.MaxValue)]
    public int DeadLetterAfterHours { get; set; } = 48;

    [Description("Lease duration, in minutes, used while a retry worker claims failed emails.")]
    [Range(1, int.MaxValue)]
    public int ClaimLeaseMinutes { get; set; } = 10;
}
