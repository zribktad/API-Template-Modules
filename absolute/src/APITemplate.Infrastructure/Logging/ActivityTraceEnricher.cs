using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace APITemplate.Infrastructure.Logging;

/// <summary>
/// Serilog <see cref="ILogEventEnricher"/> that appends W3C-format <c>TraceId</c> and <c>SpanId</c>
/// properties from the current <see cref="Activity"/> to every log event,
/// enabling correlation between structured logs and distributed traces.
/// </summary>
public sealed class ActivityTraceEnricher : ILogEventEnricher
{
    /// <summary>Reads the current ambient activity and adds <c>TraceId</c> and <c>SpanId</c> properties when non-default values are present.</summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        if (activity.TraceId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString())
            );
        }

        if (activity.SpanId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString())
            );
        }
    }
}
