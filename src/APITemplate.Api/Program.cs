using Identity.Api;
using Identity.Application.Features.User;
using ProductCatalog.Api;
using ProductCatalog.Application.Features.Product;
using Reviews.Api;
using FileStorage.Api;
using FileStorage.Application.Features.Upload;
using Reviews.Application.Features.ProductReview;
using Serilog;
using SharedKernel.Application.Middleware;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

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

builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(CreateUserCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(CreateProductsCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(CreateProductReviewCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(UploadFileCommand).Assembly);
    options.Discovery.IncludeAssembly(typeof(Program).Assembly);
    options.Policies.AddMiddleware(
        typeof(ErrorOrValidationMiddleware),
        chain =>
            chain.ShouldApplyErrorOrValidation(
                typeof(CreateUserCommand).Assembly,
                typeof(CreateProductsCommand).Assembly,
                typeof(CreateProductReviewCommand).Assembly,
                typeof(UploadFileCommand).Assembly
            )
    );
});

var app = builder.Build();

app.UseApiPipeline();
app.MapApplicationEndpoints();

app.Run();

public partial class Program;
