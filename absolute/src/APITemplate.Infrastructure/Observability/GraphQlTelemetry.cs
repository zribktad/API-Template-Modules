using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Static facade for GraphQL-specific metrics: request counts/durations, error counts by phase,
/// document and operation cache hit/miss counters, and per-operation cost histograms.
/// </summary>
public static class GraphQlTelemetry
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlRequests,
        description: "Total number of GraphQL requests observed."
    );

    private static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlErrors,
        description: "Total number of GraphQL errors observed."
    );

    private static readonly Counter<long> DocumentCacheHits = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlDocumentCacheHits,
        description: "GraphQL document cache hits."
    );

    private static readonly Counter<long> DocumentCacheMisses = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlDocumentCacheMisses,
        description: "GraphQL document cache misses."
    );

    private static readonly Counter<long> OperationCacheHits = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlOperationCacheHits,
        description: "GraphQL operation cache hits."
    );

    private static readonly Counter<long> OperationCacheMisses = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlOperationCacheMisses,
        description: "GraphQL operation cache misses."
    );

    private static readonly Histogram<double> RequestDurationMs = Meter.CreateHistogram<double>(
        TelemetryMetricNames.GraphQlRequestDuration,
        unit: "ms",
        description: "GraphQL request execution duration."
    );

    private static readonly Histogram<double> OperationCost = Meter.CreateHistogram<double>(
        TelemetryMetricNames.GraphQlOperationCost,
        description: "Computed GraphQL operation cost."
    );

    /// <summary>Increments the request counter and records duration, tagged with operation type and error status.</summary>
    public static void RecordRequest(string operationType, bool hasErrors, TimeSpan duration)
    {
        var tags = new TagList
        {
            { TelemetryTagKeys.GraphQlOperationType, operationType },
            { TelemetryTagKeys.GraphQlHasErrors, hasErrors },
        };

        Requests.Add(1, tags);
        RequestDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>Increments the error counter tagged with the request phase.</summary>
    public static void RecordRequestError() => RecordError(TelemetryGraphQlValues.RequestPhase);

    /// <summary>Increments the error counter tagged with the syntax phase.</summary>
    public static void RecordSyntaxError() => RecordError(TelemetryGraphQlValues.SyntaxPhase);

    /// <summary>Increments the error counter tagged with the validation phase.</summary>
    public static void RecordValidationError() =>
        RecordError(TelemetryGraphQlValues.ValidationPhase);

    /// <summary>Increments the error counter tagged with the resolver phase.</summary>
    public static void RecordResolverError() => RecordError(TelemetryGraphQlValues.ResolverPhase);

    /// <summary>Increments the document-cache hit counter.</summary>
    public static void RecordDocumentCacheHit() => DocumentCacheHits.Add(1);

    /// <summary>Increments the document-cache miss counter.</summary>
    public static void RecordDocumentCacheMiss() => DocumentCacheMisses.Add(1);

    /// <summary>Increments the operation-cache hit counter.</summary>
    public static void RecordOperationCacheHit() => OperationCacheHits.Add(1);

    /// <summary>Increments the operation-cache miss counter.</summary>
    public static void RecordOperationCacheMiss() => OperationCacheMisses.Add(1);

    /// <summary>
    /// Records both field-based and type-based operation cost in the cost histogram,
    /// each tagged with the corresponding cost kind.
    /// </summary>
    public static void RecordOperationCost(double fieldCost, double typeCost)
    {
        OperationCost.Record(
            fieldCost,
            new TagList
            {
                { TelemetryTagKeys.GraphQlCostKind, TelemetryGraphQlValues.FieldCostKind },
            }
        );
        OperationCost.Record(
            typeCost,
            new TagList
            {
                { TelemetryTagKeys.GraphQlCostKind, TelemetryGraphQlValues.TypeCostKind },
            }
        );
    }

    private static void RecordError(string phase)
    {
        Errors.Add(1, [new KeyValuePair<string, object?>(TelemetryTagKeys.GraphQlPhase, phase)]);
    }
}
