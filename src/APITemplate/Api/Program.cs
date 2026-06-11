using System.Reflection;
using APITemplate.Api;
using APITemplate.Api.Extensions;
using APITemplate.Api.Security;
using Asp.Versioning;
using BackgroundJobs;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Http;
using BuildingBlocks.Application.Options;
using BuildingBlocks.Infrastructure.EFCore.Persistence;
using BuildingBlocks.Infrastructure.Mongo;
using BuildingBlocks.Web.Health;
using Chatting;
using FileStorage;
using Identity;
using Identity.Auth.Security;
using JasperFx;
using JasperFx.Resources;
using Json5;
using MongoDB.Driver;
using Notifications;
using ProductCatalog;
using Reviews;
using Serilog;
using TickerQ;
using TickerQ.DependencyInjection;
using Webhooks;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Postgresql;

#region Setup & Logging

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// NEW: Explicit Configuration Ordering (JSON5 Only)
int jsonIndex = builder
    .Configuration.Sources.ToList()
    .FindIndex(s => s.GetType().Name == "JsonConfigurationSource");
if (jsonIndex < 0)
    jsonIndex = 0;

var toRemove = builder
    .Configuration.Sources.Where(s => s.GetType().Name == "JsonConfigurationSource")
    .ToList();
foreach (var s in toRemove)
    builder.Configuration.Sources.Remove(s);

var tempBuilder = new ConfigurationBuilder();
tempBuilder
    .AddJson5File("appsettings.json5", optional: false, reloadOnChange: true)
    .AddJson5File(
        $"appsettings.{builder.Environment.EnvironmentName}.json5",
        optional: true,
        reloadOnChange: true
    )
    .AddJson5File("appsettings.Identity.json5", optional: true, reloadOnChange: true)
    .AddJson5File("appsettings.Catalog.json5", optional: true, reloadOnChange: true)
    .AddJson5File("appsettings.local.json5", optional: true, reloadOnChange: true);

foreach (var source in tempBuilder.Sources.Reverse())
{
    builder.Configuration.Sources.Insert(jsonIndex, source);
}

string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

builder.WebHost.ConfigureKestrelRequestLimits(builder.Configuration);

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

#endregion

#region Service Registration

MongoSerializationConfiguration.Configure();

builder.Services.AddRequestContext();
builder.Services.AddRequestSizeLimits(builder.Configuration);
builder.Services.AddHstsRegistration(builder.Configuration);
builder.Services.AddApiVersioningRegistration();
builder.Services.AddRequestValidation();
builder.Services.AddErrorHandling(builder.Configuration);
builder.Services.AddMvcConventions();

builder.Services.AddRedisInfrastructure(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddOpenApiDocumentation();
builder.Services.AddInfrastructureDiagnostics();
builder.Services.AddGraphQLRegistration(builder.Environment);

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

#endregion

#region Wolverine Configuration

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
    options.AddDurableRetryPolicy<TimeoutException>();
});

#endregion

#region Middleware & Pipeline

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.UseDatabaseAsync(app.Lifetime.ApplicationStopping);

// TickerQ is only wired up when enabled (BackgroundJobs:TickerQ:Enabled). When disabled — e.g. in
// integration tests — neither AddTickerQ nor the registrar is registered, so UseTickerQ()/SyncAsync
// must be skipped to avoid failing host startup.
using (IServiceScope scope = app.Services.CreateScope())
{
    BackgroundJobs.TickerQ.TickerQRecurringJobRegistrar? registrar =
        scope.ServiceProvider.GetService<BackgroundJobs.TickerQ.TickerQRecurringJobRegistrar>();
    if (registrar is not null)
    {
        app.UseTickerQ();
        // Seed TickerQ recurring job definitions at startup.
        await registrar.SyncAsync(app.Lifetime.ApplicationStopping);
    }
}

app.UseApiPipeline();
app.MapApplicationEndpoints();
app.MapDeadLettersEndpoints().RequireAuthorization(AuthConstants.Policies.PlatformAdmin);

await app.RunJasperFxCommands(args);

#endregion

public partial class Program;
