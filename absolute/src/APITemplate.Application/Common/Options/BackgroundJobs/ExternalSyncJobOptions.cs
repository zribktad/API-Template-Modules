namespace APITemplate.Application.Common.Options.BackgroundJobs;

/// <summary>
/// Configuration for the scheduled job that synchronises data from external third-party systems.
/// </summary>
public sealed class ExternalSyncJobOptions
{
    public bool Enabled { get; set; }
    public string Cron { get; set; } = "0 */12 * * *";
}
