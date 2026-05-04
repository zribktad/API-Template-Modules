using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options;
using Microsoft.AspNetCore.Server.IIS;

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
    ///     Enforces request size limits in the application pipeline via <see cref="Middleware.RequestSizeLimitsMiddleware" />.
    /// </summary>
    public static IApplicationBuilder UseRequestSizeLimits(this IApplicationBuilder app)
    {
        return app.UseMiddleware<Middleware.RequestSizeLimitsMiddleware>();
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
