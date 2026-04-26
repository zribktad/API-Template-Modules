using System.Diagnostics.Metrics;

namespace APITemplate.Api.ExceptionHandling;

public sealed class ApiExceptionMetrics : IDisposable
{
    private readonly Meter _meter = new("APITemplate.Api.ExceptionHandling", "1.0.0");
    private readonly Counter<long> _mappedInfrastructureExceptions;
    private readonly Counter<long> _unhandledExceptions;

    public ApiExceptionMetrics()
    {
        _mappedInfrastructureExceptions = _meter.CreateCounter<long>(
            "api_exception_mapped_infrastructure_total"
        );
        _unhandledExceptions = _meter.CreateCounter<long>("api_exception_unhandled_total");
    }

    public void RecordMappedInfrastructureException(int statusCode, string errorCode)
    {
        _mappedInfrastructureExceptions.Add(
            1,
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("error_code", errorCode)
        );
    }

    public void RecordUnhandledException(int statusCode, string errorCode)
    {
        _unhandledExceptions.Add(
            1,
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("error_code", errorCode)
        );
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
