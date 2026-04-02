namespace APITemplate.Application.Common.Options.BackgroundJobs;

/// <summary>
/// Configuration for the background job that retries failed outbound email deliveries
/// and moves messages to the dead-letter queue after the maximum retry threshold is exceeded.
/// </summary>
public sealed class EmailRetryJobOptions
{
    public bool Enabled { get; set; }
    public string Cron { get; set; } = "*/15 * * * *";
    public int MaxRetryAttempts { get; set; } = 5;
    public int BatchSize { get; set; } = 50;
    public int DeadLetterAfterHours { get; set; } = 48;
    public int ClaimLeaseMinutes { get; set; } = 10;
}
