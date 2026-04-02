using System.Diagnostics;
using System.Security.Claims;
using APITemplate.Application.Common.Http;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Http.Features;
using Serilog.Context;

namespace APITemplate.Api.Middleware;

/// <summary>
/// Adds per-request context metadata used by logs and clients.
/// </summary>
/// <remarks>
/// In the current pipeline ordering this middleware runs after
/// <c>app.UseExceptionHandler()</c>, so thrown exceptions are still wrapped by
/// global exception handling while correlation and timing headers are maintained.
/// </remarks>
public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Creates a new <see cref="RequestContextMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Enriches the request/response with correlation, tracing, timing, and tenant metadata.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Ensures a consistent <c>X-Correlation-Id</c> is returned to clients and logged.</description></item>
    /// <item><description>Emits <c>X-Trace-Id</c> and <c>X-Elapsed-Ms</c> headers for observability.</description></item>
    /// <item><description>Adds Serilog properties for per-request logging context.</description></item>
    /// <item><description>Tags prometheus metrics (if enabled) via <see cref="IHttpMetricsTagsFeature"/>.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Ensure every request has a stable, traceable correlation ID.
        var correlationId = ResolveCorrelationId(context);

        // Track elapsed time for the full request pipeline.
        var stopwatch = Stopwatch.StartNew();

        // Prefer OpenTelemetry trace id when available, otherwise use ASP.NET trace id.
        var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        // Capture tenant id from the authenticated user (for logs/telemetry).
        var tenantId = context.User.FindFirstValue(AuthConstants.Claims.TenantId);
        var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId) ? tenantId : string.Empty;

        if (!string.IsNullOrWhiteSpace(effectiveTenantId))
        {
            // Tag the current activity for distributed tracing.
            Activity.Current?.SetTag(TelemetryTagKeys.TenantId, effectiveTenantId);
        }

        // Make the correlation ID available to downstream components.
        context.Items[RequestContextConstants.ContextKeys.CorrelationId] = correlationId;

        // Emit headers for clients/proxies to consume.
        context.Response.Headers[RequestContextConstants.Headers.CorrelationId] = correlationId;
        context.Response.Headers[RequestContextConstants.Headers.TraceId] = traceId;
        context.Response.Headers[RequestContextConstants.Headers.ElapsedMs] = "0";

        // Update elapsed-time header when the response is about to be sent.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestContextConstants.Headers.ElapsedMs] =
                stopwatch.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        try
        {
            // Enrich Serilog context so all downstream logs in this request include these properties.
            using (
                LogContext.PushProperty(
                    RequestContextConstants.LogProperties.CorrelationId,
                    correlationId
                )
            )
            using (
                LogContext.PushProperty(
                    RequestContextConstants.LogProperties.TenantId,
                    effectiveTenantId
                )
            )
            {
                await _next(context);
            }
        }
        finally
        {
            // If metrics are enabled, attach tags for the current request.
            var metricsTagsFeature = context.Features.Get<IHttpMetricsTagsFeature>();
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

    /// <summary>
    /// Resolves a correlation ID for the current request.
    /// </summary>
    /// <remarks>
    /// If the caller supplied <c>X-Correlation-Id</c>, that value is returned.
    /// Otherwise, it falls back to ASP.NET Core's <see cref="HttpContext.TraceIdentifier"/>.
    /// </remarks>
    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context
            .Request.Headers[RequestContextConstants.Headers.CorrelationId]
            .ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
            return incoming;

        return context.TraceIdentifier;
    }
}
