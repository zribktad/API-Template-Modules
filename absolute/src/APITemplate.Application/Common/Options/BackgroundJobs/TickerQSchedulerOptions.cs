namespace APITemplate.Application.Common.Options.BackgroundJobs;

/// <summary>
/// Configuration for the TickerQ scheduler, including distributed coordination and fail-safe behaviour.
/// </summary>
public sealed class TickerQSchedulerOptions
{
    public const string DefaultSchemaName = "tickerq";
    public const string DefaultCoordinationConnection = "Dragonfly";

    public bool Enabled { get; set; }
    public bool FailClosed { get; set; } = true;
    public string InstanceNamePrefix { get; set; } = "APITemplate";
    public string CoordinationConnection { get; set; } = DefaultCoordinationConnection;
}
