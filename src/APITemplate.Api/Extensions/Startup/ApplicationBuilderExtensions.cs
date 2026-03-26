using APITemplate.Api.Middleware;
using APITemplate.Application.Common.Http;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Common.Startup;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Security;
using HealthChecks.UI.Client;
using Kot.MongoDB.Migrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using TickerQ.DependencyInjection;
using TickerQ.Utilities.Enums;

namespace APITemplate.Api.Extensions.Startup;

/// <summary>
/// Presentation-layer extension class that provides <see cref="WebApplication"/> extension
/// methods for startup orchestration (database migrations, Keycloak readiness, background jobs)
/// and HTTP pipeline configuration.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Runs relational and MongoDB migrations and seeds the auth bootstrap data under a
    /// distributed advisory lock to prevent concurrent runs in multi-instance deployments.
    /// </summary>
    public static async Task UseDatabaseAsync(
        this WebApplication app,
        CancellationToken ct = default
    )
    {
        await using var scope = app.Services.CreateAsyncScope(); // Resolve scoped infra services needed only during startup migration.
        var coordinator = scope.ServiceProvider.GetRequiredService<IStartupTaskCoordinator>();

        await using var startupLease = await coordinator.AcquireAsync(
            StartupTaskName.AppBootstrap,
            ct
        );

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // Resolve EF Core context for relational migrations.
        if (dbContext.Database.IsRelational())
        {
            using var telemetry = StartupTelemetry.StartRelationalMigration();
            try
            {
                await dbContext.Database.MigrateAsync(ct);
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);
                throw;
            }
        }

        var seeder = scope.ServiceProvider.GetRequiredService<AuthBootstrapSeeder>();
        using (var telemetry = StartupTelemetry.StartAuthBootstrapSeed())
        {
            try
            {
                await seeder.SeedAsync(ct);
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);
                throw;
            }
        }

        var mongoContext = scope.ServiceProvider.GetService<MongoDbContext>(); // Mongo context can be missing in tests.
        if (mongoContext is not null)
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrator>(); // Resolve Mongo migrator from DI.
            using var telemetry = StartupTelemetry.StartMongoMigration();
            try
            {
                await migrator.MigrateAsync();
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Migrates the TickerQ scheduler store and syncs recurring job registrations when TickerQ
    /// is enabled; exits early with an informational log when disabled or unavailable.
    /// </summary>
    public static async Task UseBackgroundJobsAsync(
        this WebApplication app,
        CancellationToken ct = default
    )
    {
        var options = app.Services.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;
        if (!options.TickerQ.Enabled)
        {
            app.Logger.LogInformation("TickerQ background jobs are disabled.");
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var registrar = scope.ServiceProvider.GetService<TickerQRecurringJobRegistrar>();
        if (registrar is null)
        {
            app.Logger.LogInformation(
                "TickerQ background jobs runtime is unavailable in this host; skipping scheduler bootstrap."
            );
            return;
        }

        var schedulerDbContext = scope.ServiceProvider.GetService<TickerQSchedulerDbContext>();
        if (schedulerDbContext is null)
        {
            app.Logger.LogInformation(
                "TickerQ scheduler store is unavailable in this host; skipping scheduler bootstrap."
            );
            return;
        }

        var coordinator = scope.ServiceProvider.GetRequiredService<IStartupTaskCoordinator>();
        await using var startupLease = await coordinator.AcquireAsync(
            StartupTaskName.BackgroundJobsBootstrap,
            ct
        );

        if (schedulerDbContext.Database.IsRelational())
        {
            using var telemetry = StartupTelemetry.StartRelationalMigration();
            try
            {
                await schedulerDbContext.Database.MigrateAsync(ct);
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);
                throw;
            }
        }

        await registrar.SyncAsync(ct);

        app.UseTickerQ(TickerQStartMode.Immediate);
        app.Logger.LogInformation(
            "TickerQ background jobs started with schema {SchemaName}.",
            TickerQSchedulerOptions.DefaultSchemaName
        );
    }

    /// <summary>
    /// Cross-cutting request context: correlation ID stamping, elapsed-time header, and
    /// structured Serilog request logging. Runs early so every downstream log entry is enriched.
    /// </summary>
    public static WebApplication UseRequestContextPipeline(this WebApplication app)
    {
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
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });

        return app;
    }

    private static bool IsClientAbortedRequest(HttpContext httpContext, Exception? exception) =>
        exception is OperationCanceledException
        && httpContext.RequestAborted.IsCancellationRequested;

    /// <summary>
    /// Identity and access-control pipeline: CORS preflight handling, token/cookie
    /// authentication, CSRF enforcement for BFF cookie sessions, and authorization policy
    /// evaluation. Order is fixed — each step depends on the one before it.
    /// </summary>
    public static WebApplication UseSecurityPipeline(this WebApplication app)
    {
        app.UseCors(); // CORS preflight must precede authentication.
        app.UseAuthentication(); // Populate HttpContext.User from JWT / cookie.
        app.UseMiddleware<CsrfValidationMiddleware>(); // Require X-CSRF header for cookie-authenticated mutations.
        app.UseAuthorization(); // Enforce endpoint authorization policies.

        return app;
    }

    /// <summary>
    /// Polls the Keycloak OIDC discovery endpoint using a Polly retry pipeline, blocking
    /// startup until Keycloak is reachable or the retry budget is exhausted.
    /// </summary>
    public static async Task WaitForKeycloakAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default
    )
    {
        var keycloak = app.Services.GetRequiredService<IOptions<KeycloakOptions>>().Value;

        if (string.IsNullOrEmpty(keycloak.AuthServerUrl) || string.IsNullOrEmpty(keycloak.Realm))
        {
            app.Logger.KeycloakConfigMissing();
            return;
        }

        if (keycloak.SkipReadinessCheck)
        {
            app.Logger.KeycloakReadinessCheckSkipped();
            return;
        }

        var discoveryUrl = KeycloakUrlHelper.BuildDiscoveryUrl(
            keycloak.AuthServerUrl,
            keycloak.Realm
        );
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var pipelineProvider = app.Services.GetRequiredService<
            ResiliencePipelineProvider<string>
        >();
        var pipeline = pipelineProvider.GetPipeline(ResiliencePipelineKeys.KeycloakReadiness);

        try
        {
            using var telemetry = StartupTelemetry.StartKeycloakReadinessCheck();
            try
            {
                await pipeline.ExecuteAsync(
                    async token =>
                    {
                        var response = await httpClient.GetAsync(discoveryUrl, token);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException(
                                $"Keycloak readiness probe returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
                            );
                        }
                    },
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);
                throw;
            }

            app.Logger.KeycloakReady(keycloak.AuthServerUrl);
        }
        catch (HttpRequestException ex)
        {
            app.Logger.KeycloakUnavailable(ex, keycloak.ReadinessMaxRetries);
            throw new InvalidOperationException(
                $"Keycloak at {keycloak.AuthServerUrl} did not become available after {keycloak.ReadinessMaxRetries} retries.",
                ex
            );
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            app.Logger.KeycloakUnavailable(ex, keycloak.ReadinessMaxRetries);
            throw new InvalidOperationException(
                $"Keycloak at {keycloak.AuthServerUrl} did not become available after {keycloak.ReadinessMaxRetries} retries.",
                ex
            );
        }
    }

    /// <summary>
    /// Builds the HTTP middleware pipeline in execution order.
    /// </summary>
    /// <remarks>
    /// Exception handling is intentionally first so downstream middleware and endpoints
    /// are wrapped by the global handler. <c>app.UseExceptionHandler()</c> activates
    /// handlers previously registered in DI (for example via <c>AddExceptionHandler&lt;T&gt;()</c>).
    /// </remarks>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler(); // Global exception handling — must be outermost.
        app.UseApiDocumentation(); // Scalar / OpenAPI — development only.
        app.UseHttpsRedirection();
        app.UseSecurityPipeline(); // CORS → Authentication → CSRF → Authorization.
        app.UseRequestContextPipeline(); // Correlation enrichment + structured request logging (after auth for tenant context).
        app.UseRateLimiter();
        app.UseOutputCache();

        return app;
    }

    /// <summary>Maps REST controllers, the GraphQL endpoint, Nitro UI, and health checks.</summary>
    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapControllers().RequireRateLimiting(RateLimitPolicies.Fixed);
        app.MapGraphQL().RequireRateLimiting(RateLimitPolicies.Fixed);
        app.MapNitroApp("/graphql/ui");
        app.UseHealthChecks();

        return app;
    }

    /// <summary>
    /// Mounts the OpenAPI JSON endpoint and the Scalar interactive API reference in Development
    /// only, pre-configured with Keycloak PKCE OAuth2 authorization-code flow.
    /// </summary>
    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app; // Keep interactive API docs available only in development.

        var keycloak = app.Services.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        var appOptions = app.Services.GetRequiredService<IOptions<AppOptions>>().Value;
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);

        app.MapOpenApi().AllowAnonymous(); // Map OpenAPI JSON endpoint.
        app.MapScalarApiReference(
                "/scalar",
                (options, httpContext) =>
                {
                    var redirectUri = BuildScalarRedirectUri(httpContext.Request);

                    options.WithTitle(appOptions.ServiceName);
                    options
                        .AddPreferredSecuritySchemes(AuthConstants.OpenApi.OAuth2Scheme)
                        .AddAuthorizationCodeFlow(
                            AuthConstants.OpenApi.OAuth2Scheme,
                            flow =>
                            {
                                flow.ClientId = AuthConstants.OpenApi.ScalarClientId;
                                flow.SelectedScopes = [.. AuthConstants.Scopes.Default];
                                flow.AuthorizationUrl =
                                    $"{authority}/{AuthConstants.OpenIdConnect.AuthorizationEndpointPath}";
                                flow.TokenUrl =
                                    $"{authority}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
                                flow.RedirectUri = redirectUri;
                                flow.Pkce = Pkce.Sha256;
                            }
                        );
                }
            )
            .AllowAnonymous();

        return app;
    }

    private static string BuildScalarRedirectUri(HttpRequest request) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";

    /// <summary>Maps the <c>/health</c> endpoint with a JSON health-check UI response writer, available anonymously.</summary>
    public static WebApplication UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks(
                "/health",
                new HealthCheckOptions
                {
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                }
            )
            .WithTags("Health")
            .WithSummary("Health check")
            .WithDescription("Returns the health status of all registered services.")
            .AllowAnonymous();

        return app;
    }
}
