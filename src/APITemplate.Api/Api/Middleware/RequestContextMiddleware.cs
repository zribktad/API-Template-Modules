using System.Diagnostics;
using SharedKernel.Application.Http;
using Serilog.Context;

namespace APITemplate.Api.Middleware;

public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        context.Items[RequestContextConstants.ContextKeys.CorrelationId] = correlationId;
        context.Response.Headers[RequestContextConstants.Headers.CorrelationId] = correlationId;
        context.Response.Headers[RequestContextConstants.Headers.TraceId] = traceId;
        context.Response.Headers[RequestContextConstants.Headers.ElapsedMs] = "0";

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestContextConstants.Headers.ElapsedMs] =
                stopwatch.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(RequestContextConstants.LogProperties.CorrelationId, correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[RequestContextConstants.Headers.CorrelationId].ToString();
        return string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming;
    }
}
