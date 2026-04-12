using APITemplate.Api.Middleware;
using Chatting;
using FileStorage;
using HealthChecks.UI.Client;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notifications;
using ProductCatalog;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SharedKernel.Application.Http;
using SharedKernel.Infrastructure.Health;

namespace APITemplate.Api.Extensions.Startup;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        // Correlation in Items + Serilog only (no Response) so Challenge runs before response headers.
        app.UseMiddleware<CorrelationContextMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();
        // Response headers, tenant Serilog, metrics — after Challenge/Forbid can set status.
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
        app.MapProductCatalogEndpoints();
        app.MapFileStorageEndpoints();
        app.MapChattingEndpoints();
        app.MapNotificationsEndpoints();

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
