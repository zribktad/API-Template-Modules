using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring request-related limits and behaviors.
/// </summary>
public static class RequestSizeLimitExtensions
{
    /// <summary>
    ///     Registers and enforces request size limits for Kestrel and IIS based on RequestOptions.
    ///
    ///     Why this is needed:
    ///     While Kestrel handles requests when running standalone, IIS requires its own specific
    ///     configuration (IISServerOptions) when hosting the application. This ensures the same
    ///     global limit is applied consistently regardless of the hosting model.
    /// </summary>
    public static IServiceCollection AddRequestSizeLimits(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RequestOptions>(configuration);

        RequestOptions opts =
            configuration.SectionFor<RequestOptions>().Get<RequestOptions>() ?? new();

        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = opts.RequestSizeLimitBytes;
        });

        return services;
    }

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
    public static IApplicationBuilder UseRequestSizeLimits(this IApplicationBuilder app)
    {
        return app.Use(
            async (context, next) =>
            {
                RequestOptions opts = context
                    .RequestServices.GetRequiredService<IOptions<RequestOptions>>()
                    .Value;

                // Apply limit to the current request feature if supported (Kestrel/IIS)
                IHttpMaxRequestBodySizeFeature? maxRequestBodySizeFeature =
                    context.Features.Get<IHttpMaxRequestBodySizeFeature>();

                if (maxRequestBodySizeFeature is { IsReadOnly: false })
                {
                    maxRequestBodySizeFeature.MaxRequestBodySize = opts.RequestSizeLimitBytes;
                }

                // Manual check for Content-Length (Defense in Depth / TestServer compatibility)
                if (
                    context.Request.ContentLength > opts.RequestSizeLimitBytes
                    && !IsLimitDisabled(context)
                )
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    await context.Response.WriteAsync("Request body too large.");
                    return;
                }

                await next();
            }
        );
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

    /// <summary>
    ///     Configures Kestrel specific request limits.
    ///     Must be called on IWebHostBuilder (usually via builder.WebHost).
    ///
    ///     Why this is needed:
    ///     Kestrel is the first line of defense. By setting Limits.MaxRequestBodySize, the server
    ///     immediately rejects oversized requests at the network level (HTTP 413) before the
    ///     ASP.NET Core middleware pipeline is even invoked. This saves CPU and memory.
    /// </summary>
    public static IWebHostBuilder ConfigureKestrelRequestLimits(
        this IWebHostBuilder webHost,
        IConfiguration configuration
    )
    {
        RequestOptions opts =
            configuration.SectionFor<RequestOptions>().Get<RequestOptions>() ?? new();

        return webHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = opts.RequestSizeLimitBytes;
        });
    }
}
