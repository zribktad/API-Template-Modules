using APITemplate.Api.Middleware;
using Chatting.Api;
using FileStorage.Api;
using Microsoft.Extensions.Options;
using ProductCatalog.Api;
using Scalar.AspNetCore;
using Serilog;

namespace APITemplate.Api.Extensions.Startup;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseAuthentication();
        app.UseAuthorization();
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

        app.MapHealthChecks("/health").WithTags("Health").AllowAnonymous();
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

    private sealed record HostStatusResponse(string Service, string Status);
}
