using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace APITemplate.Api.Extensions;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddValidatedOptions<AppOptions>(configuration);
        services.AddValidatedOptions<ObservabilityOptions>(configuration);

        AppOptions appOptions =
            configuration.SectionFor<AppOptions>().Get<AppOptions>() ?? new AppOptions();
        ObservabilityOptions observabilityOptions =
            configuration.SectionFor<ObservabilityOptions>().Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        string serviceName = string.IsNullOrWhiteSpace(appOptions.ServiceName)
            ? "APITemplate"
            : appOptions.ServiceName;
        string? serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString();
        bool enableConsoleExporter =
            observabilityOptions.Exporters.Console.Enabled ?? environment.IsDevelopment();
        Uri? otlpEndpoint = ResolveOtlpEndpoint(observabilityOptions, environment);

        OpenTelemetryBuilder openTelemetryBuilder = services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(serviceName, serviceVersion: serviceVersion)
            );

        openTelemetryBuilder.WithTracing(builder =>
        {
            builder.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

            if (enableConsoleExporter)
                builder.AddConsoleExporter();

            if (otlpEndpoint is not null)
                builder.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
        });

        openTelemetryBuilder.WithMetrics(builder =>
        {
            builder.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

            if (enableConsoleExporter)
                builder.AddConsoleExporter();

            if (otlpEndpoint is not null)
                builder.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
        });

        return services;
    }

    private static Uri? ResolveOtlpEndpoint(
        ObservabilityOptions observabilityOptions,
        IHostEnvironment environment
    )
    {
        bool explicitOtlpEnabled = observabilityOptions.Exporters.Otlp.Enabled == true;
        if (
            explicitOtlpEnabled
            && Uri.TryCreate(observabilityOptions.Otlp.Endpoint, UriKind.Absolute, out Uri? otlpUri)
        )
            return otlpUri;

        bool aspireEnabled =
            observabilityOptions.Exporters.Aspire.Enabled ?? environment.IsDevelopment();
        if (
            aspireEnabled
            && Uri.TryCreate(
                observabilityOptions.Aspire.Endpoint,
                UriKind.Absolute,
                out Uri? aspireUri
            )
        )
            return aspireUri;

        return null;
    }
}
