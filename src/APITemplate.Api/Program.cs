using Serilog;

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
builder.Services.AddObservability(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApiPipeline();
app.MapApplicationEndpoints();

app.Run();

public partial class Program;
