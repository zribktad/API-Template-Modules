using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.BackgroundJobs;

/// <summary>
///     Configuration for the scheduled job that rebuilds search indexes on a periodic basis.
/// </summary>
public sealed class ReindexJobOptions
{
    [Description("Enables execution of the search reindex recurring job.")]
    public bool Enabled { get; set; }

    [Description("Five-part CRON expression that schedules the search reindex job.")]
    [Required]
    [MinLength(1)]
    public string Cron { get; set; } = "0 */6 * * *";
}
