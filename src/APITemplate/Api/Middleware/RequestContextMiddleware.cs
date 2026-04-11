using System.Diagnostics;
using System.Security.Claims;
using Identity.Security;
using Microsoft.AspNetCore.Http.Features;
using Serilog.Context;
using SharedKernel.Application.Http;
using SharedKernel.Infrastructure.Observability;

namespace APITemplate.Api.Middleware;

/// <summary>
///     After authorization: response headers (correlation, trace, elapsed), tenant enrichment,
///     and HTTP metrics tags. Correlation id is expected from <see cref="CorrelationContextMiddleware" />
///     or resolved here as a fallback (e.g. unit tests).
/// </summary>
public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationIdForRequest(context);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        string tenantId =
            context.User.FindFirstValue(AuthConstants.Claims.TenantId) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(tenantId))
            Activity.Current?.SetTag(TelemetryTagKeys.TenantId, tenantId);

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
                string.IsNullOrWhiteSpace(tenantId)
                    ? null
                    : LogContext.PushProperty(
                        RequestContextConstants.LogProperties.TenantId,
                        tenantId
                    )
            )
                await _next(context);
        }
        finally
        {
            IHttpMetricsTagsFeature? metricsTagsFeature =
                context.Features.Get<IHttpMetricsTagsFeature>();
            if (metricsTagsFeature is not null)
            {
                metricsTagsFeature.Tags.Add(
                    new KeyValuePair<string, object?>(
                        TelemetryTagKeys.ApiSurface,
                        TelemetryApiSurfaceResolver.Resolve(context.Request.Path)
                    )
                );
                metricsTagsFeature.Tags.Add(
                    new KeyValuePair<string, object?>(
                        TelemetryTagKeys.Authenticated,
                        context.User.Identity?.IsAuthenticated == true
                    )
                );
            }
        }
    }

    private static string ResolveCorrelationIdForRequest(HttpContext context)
    {
        if (
            context.Items.TryGetValue(
                RequestContextConstants.ContextKeys.CorrelationId,
                out object? existing
            )
            && existing is string s
            && !string.IsNullOrWhiteSpace(s)
        )
            return s;

        string resolved = RequestCorrelationHelper.ResolveCorrelationId(context);
        context.Items[RequestContextConstants.ContextKeys.CorrelationId] = resolved;
        return resolved;
    }
}
