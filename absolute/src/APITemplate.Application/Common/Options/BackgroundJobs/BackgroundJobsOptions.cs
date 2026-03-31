namespace APITemplate.Application.Common.Options.BackgroundJobs;

/// <summary>
/// Aggregates per-job configuration options for all registered background jobs in the application.
/// </summary>
public sealed class BackgroundJobsOptions
{
    public TickerQSchedulerOptions TickerQ { get; set; } = new();
    public ExternalSyncJobOptions ExternalSync { get; set; } = new();
    public CleanupJobOptions Cleanup { get; set; } = new();
    public ReindexJobOptions Reindex { get; set; } = new();
    public EmailRetryJobOptions EmailRetry { get; set; } = new();
}
