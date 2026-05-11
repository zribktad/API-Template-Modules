using APITemplate.Api.Middleware;
using BackgroundJobs;
using BuildingBlocks.Application.Http;
using BuildingBlocks.Web.Health;
using Chatting;
using FileStorage;
using HealthChecks.UI.Client;
using Identity;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notifications;
using ProductCatalog;
using Reviews;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Webhooks;
using Wolverine.Http;

namespace APITemplate.Api.Extensions.Startup;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();

        // Explicitly trigger routing here so the next middleware (RequestSizeLimits)
        // can read the endpoint metadata to check for custom size limits.
        //
        // WHY THIS IS NEEDED:
        // The RequestSizeLimits middleware calls context.GetEndpoint() to check
        // for IRequestSizeLimitMetadata (set by [DisableRequestSizeLimit] /
        // [RequestSizeLimit] attributes). Without UseRouting() being called
        // first, the endpoint would be null and custom limits would be ignored.
        app.UseRouting();

        app.UseRequestSizeLimits();

        app.UseSecurityHeadersPolicy();
        app.UseHttpsRedirection();
        app.UseCors();
        // Correlation in Items + Serilog only (no Response) so Challenge runs before response headers.
        app.UseMiddleware<CorrelationContextMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();
        // After UseAuthorization so the GlobalLimiter can read context.User for per-user partitioning.
        // Before RequestContextMiddleware and OutputCache so rejected requests never reach the cache layer.
        app.UseRateLimiter();
        // Response headers, tenant Serilog, metrics — after Challenge/Forbid can set status.
        app.UseMiddleware<RequestContextMiddleware>();
        // Replaces the standard ASP.NET Core request logging with a high-signal consolidated log.
        // - GetLevel: Dynamically adjusts log level (Error for 500s/exceptions, Warning for 400s,
        //   Information for others). Suppresses Error logs for client-aborted requests.
        // - MessageTemplate: Standardizes the log output into a single-line summary with timing.
        // - EnrichDiagnosticContext: Injects infrastructure metadata (Host, Scheme) into the
        //   structured log payload for better traceability in Loki/Grafana.
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

        foreach (
            HealthCheckEndpointDefinition endpoint in HealthCheckEndpointConfiguration.Endpoints
        )
        {
            HealthCheckOptions options = new()
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                Predicate = endpoint.Predicate,
            };

            app.MapHealthChecks(endpoint.Path, options)
                .WithTags(HealthCheckTags.OpenApiTag)
                .AllowAnonymous();
        }

        app.MapControllers();
        RouteGroupBuilder wolverineGroup = app.MapGroup("");
        wolverineGroup.AddEndpointFilter<BuildingBlocks.Web.Api.Filters.PaginationFilter>();
        wolverineGroup.MapWolverineEndpoints(opts =>
        {
            opts.UseDataAnnotationsValidationProblemDetailMiddleware();
        });

        app.MapGraphQL();

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(
                "/scalar",
                options =>
                {
                    options
                        .AddPreferredSecuritySchemes(
                            AuthConstants.OpenApi.OAuth2ScalarScheme,
                            AuthConstants.OpenApi.OAuth2PublicScheme
                        )
                        .AddAuthorizationCodeFlow(
                            AuthConstants.OpenApi.OAuth2ScalarScheme,
                            flow =>
                            {
                                flow.ClientId = AuthConstants.OpenApi.ScalarClientId;
                                flow.Pkce = Pkce.Sha256;
                                flow.SelectedScopes =
                                [
                                    AuthConstants.Scopes.OpenId,
                                    AuthConstants.Scopes.Profile,
                                    AuthConstants.Scopes.Email,
                                ];
                            }
                        )
                        .AddAuthorizationCodeFlow(
                            AuthConstants.OpenApi.OAuth2PublicScheme,
                            flow =>
                            {
                                flow.ClientId = AuthConstants.OpenApi.PublicClientId;
                                flow.Pkce = Pkce.Sha256;
                                flow.SelectedScopes =
                                [
                                    AuthConstants.Scopes.OpenId,
                                    AuthConstants.Scopes.Profile,
                                    AuthConstants.Scopes.Email,
                                ];
                            }
                        );
                }
            )
            .AllowAnonymous();
        return app;
    }

    private static bool IsClientAbortedRequest(HttpContext httpContext, Exception? exception)
    {
        return exception is OperationCanceledException oce
            && oce.CancellationToken == httpContext.RequestAborted;
    }

    private sealed record HostStatusResponse(string Service, string Status);
}
