using System.Diagnostics.Metrics;
using BuildingBlocks.Web.Observability;
using Microsoft.Extensions.Diagnostics;

namespace APITemplate.Api.ExceptionHandling;

public sealed class ApiExceptionMetrics : IDisposable
{
    public const string MeterName = "APITemplate.Api.ExceptionHandling";

    private readonly Meter _meter;
    private readonly Counter<long> _mappedInfrastructureExceptions;
    private readonly Counter<long> _unhandledExceptions;

    public ApiExceptionMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName, "1.0.0");
        _mappedInfrastructureExceptions = _meter.CreateCounter<long>(
            "api_exception_mapped_infrastructure_total"
        );
        _unhandledExceptions = _meter.CreateCounter<long>("api_exception_unhandled_total");
    }

    public void RecordMappedInfrastructureException(int statusCode, string errorCode)
    {
        _mappedInfrastructureExceptions.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryTagKeys.HttpResponseStatusCode, statusCode),
            new KeyValuePair<string, object?>(TelemetryTagKeys.ErrorCode, errorCode)
        );
    }

    public void RecordUnhandledException(int statusCode, string errorCode)
    {
        _unhandledExceptions.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryTagKeys.HttpResponseStatusCode, statusCode),
            new KeyValuePair<string, object?>(TelemetryTagKeys.ErrorCode, errorCode)
        );
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
