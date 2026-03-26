using APITemplate.Application.Common.Middleware;
using JasperFx;
using JasperFx.CodeGeneration;
using Serilog;
using Wolverine;

try
{
    var builder = WebApplication.CreateBuilder(args); // Build host, configuration, and DI container.
    builder.AddApplicationRedaction();

    builder.Host.UseSerilog(
        (context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .AddOpenTelemetrySinks(context.Configuration, context.HostingEnvironment);
        }
    );

    builder.Services.AddApiFoundation(builder.Configuration); // Registers exception handling services (AddExceptionHandler + ProblemDetails), activated later in UseApiPipeline.
    builder.Services.AddObservability(builder.Configuration, builder.Environment); // Register OpenTelemetry tracing/metrics and environment-specific exporters.
    builder.Services.AddAuthenticationOptions(builder.Configuration, builder.Environment);
    builder.Services.AddPersistence(builder.Configuration); // Register EF Core + repositories + relational health checks.
    builder.Services.AddApplicationServices(); // Register application services + validators.
    builder.Services.AddEmailServices(builder.Configuration); // Register email sending infrastructure (SMTP, templates, queue, background service).
    builder.Services.AddBackgroundJobs(builder.Configuration); // Register TickerQ-backed recurring background jobs (cleanup, reindex, email retry).
    builder.Services.AddMongoDB(builder.Configuration); // Register Mongo context/services + Mongo health checks.
    builder.Services.AddKeycloakBffAuthentication(builder.Configuration, builder.Environment); // Register Keycloak hybrid JWT + BFF authentication.
    builder.Services.AddKeycloakAdminService(); // Register Keycloak Admin API client for user management.
    builder.Services.AddApiVersioningConfiguration(); // Register API versioning and explorer metadata.
    builder.Services.AddGraphQLConfiguration(); // Register GraphQL schema and server services.
    builder.Services.AddFileStorageServices(builder.Configuration); // Register file storage (local FS) for example upload/download.
    builder.Services.AddJobServices(); // Register long-running job queue and background processor.
    builder.Services.AddIncomingWebhookServices(builder.Configuration); // Register webhook HMAC validation, queue, and background processor.
    builder.Services.AddOutgoingWebhookServices(); // Register outgoing webhook queue, signer, and delivery background service.

    builder.Services.CritterStackDefaults(x =>
    {
        x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        x.Production.AssertAllPreGeneratedTypesExist = true;
        x.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
    });

    builder.Host.UseWolverine(opts =>
    {
        opts.Durability.Mode = DurabilityMode.Balanced;
        opts.Discovery.IncludeAssembly(typeof(CreateProductsCommand).Assembly);
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

        // Apply ErrorOr validation middleware only to handlers returning ErrorOr<T>.
        // Event handlers (returning Task) and non-ErrorOr handlers are not affected.
        opts.Policies.AddMiddleware(
            typeof(ErrorOrValidationMiddleware),
            chain => chain.ShouldApplyErrorOrValidation(typeof(CreateProductsCommand).Assembly)
        );
    });

    var app = builder.Build(); // Materialize the web app from configured services.
    app.Logger.LogInformation("Starting APITemplate"); // Startup banner for diagnostics after logging pipeline is ready.

    await app.UseDatabaseAsync(app.Lifetime.ApplicationStopping); // Apply SQL/Mongo migrations before serving traffic.
    await app.WaitForKeycloakAsync(app.Lifetime.ApplicationStopping); // Wait for Keycloak to be reachable before serving traffic.
    await app.UseBackgroundJobsAsync(app.Lifetime.ApplicationStopping); // Sync and start recurring TickerQ jobs after dependencies are ready.

    app.UseApiPipeline(); // Configure middleware order for request processing.
    app.MapApplicationEndpoints(); // Map REST/GraphQL/health endpoints.

    app.Run(); // Start HTTP server and block until shutdown.
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Console.Error.WriteLine($"Application terminated unexpectedly: {ex}");
    throw;
}

/// <summary>
/// Application entry-point marker class; declared as a partial so integration tests can
/// reference the assembly via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program; // Used by integration tests via WebApplicationFactory.
