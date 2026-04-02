using APITemplate.Api.Middleware;
using Chatting.Api;
using FileStorage.Api;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ProductCatalog.Api;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SharedKernel.Application.Http;

namespace APITemplate.Api.Extensions.Startup;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, _, exception) =>
            {
                if (IsClientAbortedRequest(httpContext, exception))
                    return LogEventLevel.Information;

                if (exception is not null || httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= 400)
                    return LogEventLevel.Warning;

                return LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set(
                    RequestContextConstants.LogProperties.RequestHost,
                    httpContext.Request.Host.Value
                );
                diagnosticContext.Set(
                    RequestContextConstants.LogProperties.RequestScheme,
                    httpContext.Request.Scheme
                );
            };
        });
        app.UseOutputCache();
        app.UseApiDocumentation();

        return app;
    }

    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/",
                (IOptions<AppOptions> options) =>
                    TypedResults.Ok(new HostStatusResponse(options.Value.ServiceName, "ready"))
            )
            .WithName("HostStatus")
            .WithTags("Host");

        app.MapHealthChecks(
                "/health",
                new HealthCheckOptions
                {
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                }
            )
            .WithTags("Health")
            .AllowAnonymous();
        app.MapProductCatalogEndpoints();
        app.MapFileStorageEndpoints();
        app.MapChattingEndpoints();

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference("/scalar").AllowAnonymous();
        return app;
    }

    private static bool IsClientAbortedRequest(HttpContext httpContext, Exception? exception) =>
        exception is OperationCanceledException oce
        && oce.CancellationToken == httpContext.RequestAborted;

    private sealed record HostStatusResponse(string Service, string Status);
}
