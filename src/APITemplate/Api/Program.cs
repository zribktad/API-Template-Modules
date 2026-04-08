using System.Reflection;
using APITemplate.Api;
using BackgroundJobs;
using Chatting;
using FileStorage;
using Identity;
using JasperFx;
using JasperFx.Resources;
using Notifications;
using ProductCatalog;
using Reviews;
using Serilog;
using SharedKernel.Application.Middleware;
using SharedKernel.Infrastructure.Health;
using Webhooks;
using Wolverine;
using Wolverine.EntityFrameworkCore;
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

builder.Services.AddApiFoundation(builder.Configuration);
builder.Services.AddModuleHealthChecks(builder.Configuration, HealthCheckModuleRegistry.Modules);
builder.Services.AddApplicationComposition(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Services.AddIdentityModule(builder.Configuration);
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
    options.PersistMessagesWithPostgresql(connectionString);

    // Only activates for handlers with a DbContext enrolled via AddDbContextWithWolverineIntegration.
    options.UseEntityFrameworkCoreTransactions();

    // UseDurableLocalQueues persists cascading messages in PostgreSQL so they survive a crash
    // between handler commit and message dispatch. UseStrictLocalQueues would additionally
    // guarantee in-order processing per queue — not needed here since handlers are idempotent.
    options.Policies.UseDurableLocalQueues();
    foreach (Assembly assembly in WolverineModuleDiscovery.HandlerAssemblies)
        options.Discovery.IncludeAssembly(assembly);

    options.Policies.AddMiddleware(
        typeof(ErrorOrValidationMiddleware),
        chain =>
            chain.ShouldApplyErrorOrValidation(WolverineModuleDiscovery.ErrorOrValidationAssemblies)
    );
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.UseDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseApiPipeline();
app.MapApplicationEndpoints();

await app.RunJasperFxCommands(args);

public partial class Program;
