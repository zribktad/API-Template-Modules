using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BackgroundJobs.Options;

/// <summary>
///     Configuration for the TickerQ scheduler, including distributed coordination and fail-safe behaviour.
/// </summary>
public sealed class TickerQSchedulerOptions
{
    public const string DefaultSchemaName = "tickerq";
    public const string DefaultCoordinationConnection = "Dragonfly";

    [Description("Enables the TickerQ scheduler runtime.")]
    public bool Enabled { get; set; }

    [Description(
        "When true, scheduling fails closed if distributed coordination cannot be established."
    )]
    public bool FailClosed { get; set; } = true;

    [Description("Prefix used when generating scheduler instance names.")]
    [Required]
    [MinLength(1)]
    public string InstanceNamePrefix { get; set; } = "APITemplate";

    [Description("Named connection used for distributed scheduler coordination.")]
    [Required]
    [MinLength(1)]
    public string CoordinationConnection { get; set; } = DefaultCoordinationConnection;
}
