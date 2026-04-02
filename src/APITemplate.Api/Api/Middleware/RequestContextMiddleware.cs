using System.Diagnostics;
using System.Security.Claims;
using Identity.Application.Common.Security;
using Microsoft.AspNetCore.Http.Features;
using Serilog.Context;
using SharedKernel.Application.Http;
using SharedKernel.Infrastructure.Observability;

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

        string tenantId =
            context.User.FindFirstValue(AuthConstants.Claims.TenantId) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(tenantId))
            Activity.Current?.SetTag(TelemetryTagKeys.TenantId, tenantId);

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

        try
        {
            using (
                LogContext.PushProperty(
                    RequestContextConstants.LogProperties.CorrelationId,
                    correlationId
                )
            )
            using (
                LogContext.PushProperty(RequestContextConstants.LogProperties.TenantId, tenantId)
            )
            {
                await _next(context);
            }
        }
        finally
        {
            IHttpMetricsTagsFeature? metricsTagsFeature =
                context.Features.Get<IHttpMetricsTagsFeature>();
            if (metricsTagsFeature is not null)
            {
                metricsTagsFeature.Tags.Add(
                    new(
                        TelemetryTagKeys.ApiSurface,
                        TelemetryApiSurfaceResolver.Resolve(context.Request.Path)
                    )
                );
                metricsTagsFeature.Tags.Add(
                    new(
                        TelemetryTagKeys.Authenticated,
                        context.User.Identity?.IsAuthenticated == true
                    )
                );
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context
            .Request.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString();
        return string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming;
    }
}
