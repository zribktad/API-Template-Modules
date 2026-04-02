using BackgroundJobs.Api;
using BackgroundJobs.Application.Features.Jobs;
using Chatting.Api;
using Chatting.Application.Features.Streaming.Queries;
using FileStorage.Api;
using FileStorage.Application.Features.Upload;
using Identity.Api;
using Identity.Application.Features.User;
using Identity.Infrastructure.Handlers;
using JasperFx;
using JasperFx.Resources;
using ProductCatalog.Api;
using ProductCatalog.Application.Features.Product;
using ProductCatalog.Infrastructure.Handlers;
using Reviews.Api;
using Reviews.Application.Features.ProductReview;
using Serilog;
using SharedKernel.Application.Middleware;
using Webhooks.Api;
using Webhooks.Application.Handlers;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddApplicationComposition(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddProductCatalogModule(builder.Configuration);
builder.Services.AddReviewsModule(builder.Configuration);
builder.Services.AddFileStorageModule(builder.Configuration);
builder.Services.AddBackgroundJobsModule(builder.Configuration);
builder.Services.AddWebhooksModule(builder.Configuration);
builder.Services.AddChattingModule(builder.Configuration);

// Auto-create Wolverine schema tables (incoming/outgoing/dead-letter) on startup.
// Scoped to Development only — in other environments run `db-apply` as a pre-deployment step.
if (builder.Environment.IsDevelopment())
    builder.Host.UseResourceSetupOnStartup();

builder.Host.UseWolverine(options =>
{
    options.PersistMessagesWithPostgresql(connectionString);

    // Enlist every handler that depends on a DbContext (enrolled via AddDbContextWithWolverineIntegration)
    // in Wolverine's EF Core transactional middleware. Handlers without a DbContext dependency are unaffected.
    options.UseEntityFrameworkCoreTransactions();

    // UseDurableLocalQueues persists cascading messages in PostgreSQL so they survive a crash
    // between handler commit and message dispatch. UseStrictLocalQueues would additionally
    // guarantee in-order processing per queue — not needed here since handlers are idempotent.
    options.Policies.UseDurableLocalQueues();
    options.Discovery.IncludeAssembly(typeof(CreateUserCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(CreateProductsCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(CreateProductReviewCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(UploadFileCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(SubmitJobCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(CleanupExpiredInvitationsHandler).Assembly);
    options.Discovery.IncludeAssembly(typeof(CleanupOrphanedProductDataHandler).Assembly);
    options.Discovery.IncludeAssembly(typeof(SendWebhookCallbackHandler).Assembly);
    options.Discovery.IncludeAssembly(typeof(GetNotificationStreamQuery).Assembly);
    options.Policies.AddMiddleware(
        typeof(ErrorOrValidationMiddleware),
        chain =>
            chain.ShouldApplyErrorOrValidation(
                typeof(CreateUserCommand).Assembly,
                typeof(CreateProductsCommand).Assembly,
                typeof(CreateProductReviewCommand).Assembly,
                typeof(UploadFileCommand).Assembly,
                typeof(SubmitJobCommand).Assembly
            )
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.UseDatabaseAsync();

app.UseApiPipeline();
app.MapApplicationEndpoints();

await app.RunJasperFxCommands(args);

public partial class Program;
