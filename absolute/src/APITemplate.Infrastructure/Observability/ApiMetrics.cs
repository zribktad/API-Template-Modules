using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Static facade for custom API-level metrics emitted via the shared application meter.
/// Records rate-limit rejections and handled exception counts with relevant dimensional tags.
/// </summary>
public static class ApiMetrics
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> RateLimitRejections = Meter.CreateCounter<long>(
        TelemetryMetricNames.RateLimitRejections,
        description: "Total number of rejected requests by the ASP.NET Core rate limiter."
    );

    private static readonly Counter<long> HandledExceptions = Meter.CreateCounter<long>(
        TelemetryMetricNames.HandledExceptions,
        description: "Total number of exceptions translated to API responses."
    );

    /// <summary>
    /// Increments the rate-limit rejection counter, tagged with the policy name, HTTP method, and endpoint route.
    /// </summary>
    public static void RecordRateLimitRejection(string policy, string method, string endpoint)
    {
        RateLimitRejections.Add(
            1,
            [
                new KeyValuePair<string, object?>(TelemetryTagKeys.RateLimitPolicy, policy),
                new KeyValuePair<string, object?>(TelemetryTagKeys.HttpMethod, method),
                new KeyValuePair<string, object?>(TelemetryTagKeys.HttpRoute, endpoint),
            ]
        );
    }

    /// <summary>
    /// Increments the handled-exception counter, tagged with the HTTP status code, error code, and exception type.
    /// </summary>
    public static void RecordHandledException(
        int statusCode,
        string errorCode,
        string exceptionType
    )
    {
        HandledExceptions.Add(
            1,
            [
                new KeyValuePair<string, object?>(TelemetryTagKeys.ErrorCode, errorCode),
                new KeyValuePair<string, object?>(
                    TelemetryTagKeys.HttpResponseStatusCode,
                    statusCode
                ),
                new KeyValuePair<string, object?>(TelemetryTagKeys.ExceptionType, exceptionType),
            ]
        );
    }
}
