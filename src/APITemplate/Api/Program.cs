using System.Reflection;
using APITemplate.Api;
using APITemplate.Api.Extensions;
using APITemplate.Api.Security;
using Asp.Versioning;
using BackgroundJobs;
using Chatting;
using FileStorage;
using Identity;
using Identity.Auth.Security;
using JasperFx;
using JasperFx.Resources;
using MongoDB.Driver;
using Notifications;
using ProductCatalog;
using Reviews;
using Serilog;
using SharedKernel.Application.Context;
using SharedKernel.Application.Http;
using SharedKernel.Infrastructure.Health;
using Webhooks;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Postgresql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

builder.AddApplicationRedaction();

builder.Host.UseSerilog(
    (context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    }
);

builder.Services.AddRequestContext();
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddApiVersioningRegistration();
builder.Services.AddRequestValidation();
builder.Services.AddErrorHandling(builder.Configuration);
builder.Services.AddMvcConventions();

builder.Services.AddRedisInfrastructure(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddOpenApiDocumentation();

builder.Services.AddWolverineHttp();
builder.Services.AddModuleHealthChecks(
    builder.Configuration,
    builder.Environment,
    HealthCheckModuleRegistry.Modules
);

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Services.AddProductCatalogModule(builder.Configuration);
builder.Services.AddReviewsModule(builder.Configuration);
builder.Services.AddFileStorageModule(builder.Configuration);
builder.Services.AddBackgroundJobsModule(builder.Configuration);
builder.Services.AddWebhooksModule(builder.Configuration);
builder.Services.AddChattingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// Auto-create Wolverine schema tables (incoming/outgoing/dead-letter) on startup.
// Scoped to Development only — in other environments run `db-apply` as a pre-deployment step.
if (builder.Environment.IsDevelopment())
    builder.Host.UseResourceSetupOnStartup();

builder.Host.UseWolverine(options =>
{
    if (builder.Environment.IsDevelopment())
        options.Durability.Mode = DurabilityMode.Solo;

    options.PersistMessagesWithPostgresql(connectionString);

    // Only activates for handlers with a DbContext enrolled via AddDbContextWithWolverineIntegration.
    options.UseEntityFrameworkCoreTransactions();

    // UseDurableLocalQueues persists cascading messages in PostgreSQL so they survive a crash
    // between handler commit and message dispatch. UseStrictLocalQueues would additionally
    // guarantee in-order processing per queue — not needed here since handlers are idempotent.
    options.Policies.UseDurableLocalQueues();

    // Persist outgoing messages in PostgreSQL before sending — guarantees at-least-once delivery
    // even if the process crashes between committing the handler and dispatching the message.
    options.Policies.UseDurableOutboxOnAllSendingEndpoints();

    // Persist incoming messages from external transports in PostgreSQL before processing —
    // prevents message loss if the process crashes before the handler completes.
    options.Policies.UseDurableInboxOnAllListeners();
    foreach (Assembly assembly in WolverineModuleDiscovery.HandlerAssemblies)
        options.Discovery.IncludeAssembly(assembly);

    // Retry policy for transient HTTP failures (e.g. Keycloak temporarily unavailable).
    // Applies only to queue-delivered messages (outbox workers) — NOT to InvokeAsync calls.
    // After all retries are exhausted the message moves to wolverine_dead_letters in PostgreSQL.
    options.AddDurableRetryPolicy<HttpRequestException>();
    options.AddDurableRetryPolicy<MongoException>();
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.UseDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseApiPipeline();
app.MapApplicationEndpoints();
app.MapDeadLettersEndpoints().RequireAuthorization(AuthConstants.Policies.PlatformAdmin);

await app.RunJasperFxCommands(args);

public partial class Program;
