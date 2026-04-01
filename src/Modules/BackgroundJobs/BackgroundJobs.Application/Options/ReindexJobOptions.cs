namespace BackgroundJobs.Application.Options;

/// <summary>
/// Configuration for the scheduled job that rebuilds search indexes on a periodic basis.
/// </summary>
public sealed class ReindexJobOptions
{
    public bool Enabled { get; set; }
    public string Cron { get; set; } = "0 */6 * * *";
}
