using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Enforces request size limits in the application pipeline.
///     This provides defense-in-depth and ensures limits are respected even in TestServer.
///
///     Why this is needed:
///     1. TestServer (used for integration testing) bypasses Kestrel. This middleware ensures
///        our tests still accurately validate size constraints.
///     2. It provides fallback validation by explicitly checking the Content-Length header
///        before resource-intensive model binding starts.
///     3. It respects endpoint-specific metadata (e.g., [DisableRequestSizeLimit]).
/// </summary>
public sealed class RequestSizeLimitsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestSizeLimitsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        RequestOptions opts = context
            .RequestServices.GetRequiredService<IOptions<RequestOptions>>()
            .Value;

        // Apply limit to the current request feature if supported (Kestrel/IIS)
        IHttpMaxRequestBodySizeFeature? maxRequestBodySizeFeature =
            context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        // Check if the current endpoint has a custom limit attribute
        bool hasCustomLimit = IsLimitDisabled(context);

        // Only override the max request body size if the endpoint doesn't define its own custom rule
        if (!hasCustomLimit && maxRequestBodySizeFeature is { IsReadOnly: false })
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = opts.RequestSizeLimitBytes;
        }

        // Manual check for Content-Length (Defense in Depth / TestServer compatibility)
        if (!hasCustomLimit && context.Request.ContentLength > opts.RequestSizeLimitBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsync("Request body too large.");
            return;
        }

        await _next(context);
    }

    private static bool IsLimitDisabled(HttpContext context)
    {
        Endpoint? endpoint = context.GetEndpoint();
        IRequestSizeLimitMetadata? metadata =
            endpoint?.Metadata.GetMetadata<IRequestSizeLimitMetadata>();

        // If metadata is present, the endpoint has an explicit limit or has it disabled (MaxRequestBodySize is null).
        // In either case, we skip the global middleware enforcement to let the endpoint's specific limit (or lack thereof) take precedence.
        return metadata != null;
    }
}
