using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Static facade for conflict-related metrics, distinguishing optimistic-concurrency
/// EF Core exceptions from domain-layer conflict exceptions.
/// </summary>
public static class ConflictTelemetry
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> ConcurrencyConflicts = Meter.CreateCounter<long>(
        TelemetryMetricNames.ConcurrencyConflicts,
        description: "Number of optimistic concurrency conflicts."
    );

    private static readonly Counter<long> DomainConflicts = Meter.CreateCounter<long>(
        TelemetryMetricNames.DomainConflicts,
        description: "Number of domain conflict responses."
    );

    /// <summary>
    /// Increments the appropriate conflict counter based on the exception type.
    /// EF Core concurrency exceptions increment the concurrency counter; domain
    /// <see cref="Domain.Exceptions.ConflictException"/> increments the domain-conflicts counter tagged with <paramref name="errorCode"/>.
    /// </summary>
    public static void Record(Exception exception, string errorCode)
    {
        if (exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            ConcurrencyConflicts.Add(1);
            return;
        }

        if (exception is Domain.Exceptions.ConflictException)
        {
            DomainConflicts.Add(
                1,
                [new KeyValuePair<string, object?>(TelemetryTagKeys.ErrorCode, errorCode)]
            );
        }
    }
}
