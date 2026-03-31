using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Observability;
using HotChocolate.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that configures OpenTelemetry tracing, metrics, and
/// OTLP/console exporters, as well as health-check metrics publishing.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry with ASP.NET Core, HttpClient, runtime, GraphQL, Redis, and
    /// Npgsql instrumentation; configures custom histogram boundaries and environment-aware
    /// OTLP exporters (Aspire in dev, container OTLP otherwise).
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.Configure<ObservabilityOptions>(configuration.SectionFor<ObservabilityOptions>());
        services.Configure<AppOptions>(configuration.SectionFor<AppOptions>());
        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(15);
            options.Period = TimeSpan.FromMinutes(5);
        });

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var options = GetObservabilityOptions(configuration);
        var appOptions = GetAppOptions(configuration);
        var resourceAttributes = BuildResourceAttributes(appOptions, environment);
        var enableConsoleExporter = IsConsoleExporterEnabled(options);
        var otlpEndpoints = GetEnabledOtlpEndpoints(options, environment);

        var openTelemetryBuilder = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(resourceAttributes));

        openTelemetryBuilder.WithTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments(TelemetryPathPrefixes.Health);
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        if (
                            TelemetryApiSurfaceResolver.Resolve(httpRequest.Path)
                            != TelemetrySurfaces.Rest
                        )
                            return;

                        var route = HttpRouteResolver.Resolve(httpRequest.HttpContext);
                        activity.DisplayName = $"{httpRequest.Method} {route}";
                        activity.SetTag(TelemetryTagKeys.HttpRoute, route);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddHotChocolateInstrumentation()
                .AddRedisInstrumentation()
                .AddNpgsql()
                .AddSource(ObservabilityConventions.ActivitySourceName)
                .AddSource(TelemetryThirdPartySources.MongoDbDriverDiagnosticSources)
                .AddSource(TelemetryThirdPartySources.Wolverine);

            ConfigureTracingExporters(builder, otlpEndpoints, enableConsoleExporter);
        });

        openTelemetryBuilder.WithMetrics(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter(
                    ObservabilityConventions.MeterName,
                    ObservabilityConventions.HealthMeterName,
                    TelemetryMeterNames.AspNetCoreHosting,
                    TelemetryMeterNames.AspNetCoreServerKestrel,
                    TelemetryMeterNames.AspNetCoreConnections,
                    TelemetryMeterNames.AspNetCoreRouting,
                    TelemetryMeterNames.AspNetCoreDiagnostics,
                    TelemetryMeterNames.AspNetCoreRateLimiting,
                    TelemetryMeterNames.AspNetCoreAuthentication,
                    TelemetryMeterNames.AspNetCoreAuthorization,
                    TelemetryThirdPartySources.Wolverine
                )
                .AddView(
                    TelemetryInstrumentNames.HttpServerRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = TelemetryHistogramBoundaries.HttpRequestDurationSeconds,
                    }
                )
                .AddView(
                    TelemetryInstrumentNames.HttpClientRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = TelemetryHistogramBoundaries.HttpRequestDurationSeconds,
                    }
                )
                .AddView(
                    TelemetryMetricNames.OutputCacheInvalidationDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = TelemetryHistogramBoundaries.CacheOperationDurationMs,
                    }
                )
                .AddView(
                    TelemetryMetricNames.GraphQlRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = TelemetryHistogramBoundaries.GraphQlRequestDurationMs,
                    }
                );

            ConfigureMetricExporters(builder, otlpEndpoints, enableConsoleExporter);
        });

        return services;
    }

    /// <summary>
    /// Returns the distinct set of OTLP endpoint URLs that are enabled for the current environment,
    /// combining Aspire (dev default) and explicit OTLP (container default) endpoints.
    /// </summary>
    internal static IReadOnlyList<string> GetEnabledOtlpEndpoints(
        ObservabilityOptions options,
        IHostEnvironment environment
    )
    {
        var endpoints = new List<string>();

        if (IsAspireExporterEnabled(options, environment))
        {
            var aspireEndpoint = string.IsNullOrWhiteSpace(options.Aspire.Endpoint)
                ? TelemetryDefaults.AspireOtlpEndpoint
                : options.Aspire.Endpoint;
            endpoints.Add(aspireEndpoint);
        }

        if (
            IsOtlpExporterEnabled(options, environment)
            && !string.IsNullOrWhiteSpace(options.Otlp.Endpoint)
        )
        {
            endpoints.Add(options.Otlp.Endpoint);
        }

        return endpoints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Returns whether the Aspire OTLP exporter is active: uses the explicit configuration value
    /// when set, otherwise defaults to <see langword="true"/> in Development outside a container.
    /// </summary>
    internal static bool IsAspireExporterEnabled(
        ObservabilityOptions options,
        IHostEnvironment environment
    ) =>
        options.Exporters.Aspire.Enabled
        ?? (environment.IsDevelopment() && !IsRunningInContainer());

    /// <summary>
    /// Returns whether the generic OTLP exporter is active: uses the explicit configuration value
    /// when set, otherwise defaults to <see langword="true"/> when running in a container.
    /// </summary>
    internal static bool IsOtlpExporterEnabled(
        ObservabilityOptions options,
        IHostEnvironment environment
    ) => options.Exporters.Otlp.Enabled ?? IsRunningInContainer();

    /// <summary>Returns whether the console/stdout exporter is enabled; defaults to <see langword="false"/>.</summary>
    internal static bool IsConsoleExporterEnabled(ObservabilityOptions options) =>
        options.Exporters.Console.Enabled ?? false;

    private static bool IsRunningInContainer() =>
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>Reads and binds <see cref="ObservabilityOptions"/> from configuration, returning defaults when absent.</summary>
    internal static ObservabilityOptions GetObservabilityOptions(IConfiguration configuration) =>
        configuration.SectionFor<ObservabilityOptions>().Get<ObservabilityOptions>() ?? new();

    /// <summary>Reads and binds <see cref="AppOptions"/> from configuration, returning defaults when absent.</summary>
    internal static AppOptions GetAppOptions(IConfiguration configuration) =>
        configuration.SectionFor<AppOptions>().Get<AppOptions>() ?? new();

    /// <summary>
    /// Builds the OpenTelemetry resource attribute dictionary including service name, version,
    /// instance ID, host, architecture, OS, and runtime metadata.
    /// </summary>
    internal static Dictionary<string, object> BuildResourceAttributes(
        AppOptions appOptions,
        IHostEnvironment environment
    )
    {
        var serviceName = string.IsNullOrWhiteSpace(appOptions.ServiceName)
            ? ObservabilityConventions.ActivitySourceName
            : appOptions.ServiceName;
        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName().Name ?? serviceName;
        var version = entryAssembly?.GetName().Version?.ToString() ?? TelemetryDefaults.Unknown;
        var machineName = Environment.MachineName;
        var processId = Environment.ProcessId;

        return new Dictionary<string, object>
        {
            [TelemetryResourceAttributeKeys.AssemblyName] = assemblyName,
            [TelemetryResourceAttributeKeys.ServiceName] = serviceName,
            [TelemetryResourceAttributeKeys.ServiceNamespace] = serviceName,
            [TelemetryResourceAttributeKeys.ServiceVersion] = version,
            [TelemetryResourceAttributeKeys.ServiceInstanceId] = $"{machineName}-{processId}",
            [TelemetryResourceAttributeKeys.DeploymentEnvironmentName] =
                environment.EnvironmentName,
            [TelemetryResourceAttributeKeys.HostName] = machineName,
            [TelemetryResourceAttributeKeys.HostArchitecture] =
                RuntimeInformation.OSArchitecture.ToString(),
            [TelemetryResourceAttributeKeys.OsType] = GetOsType(),
            [TelemetryResourceAttributeKeys.ProcessPid] = processId,
            [TelemetryResourceAttributeKeys.ProcessRuntimeName] = ".NET",
            [TelemetryResourceAttributeKeys.ProcessRuntimeVersion] = Environment.Version.ToString(),
        };
    }

    private static string GetOsType() =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "darwin"
        : TelemetryDefaults.Unknown;

    private static void ConfigureTracingExporters(
        TracerProviderBuilder builder,
        IReadOnlyList<string> otlpEndpoints,
        bool enableConsoleExporter
    )
    {
        foreach (var endpoint in otlpEndpoints)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        if (enableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }

    private static void ConfigureMetricExporters(
        MeterProviderBuilder builder,
        IReadOnlyList<string> otlpEndpoints,
        bool enableConsoleExporter
    )
    {
        foreach (var endpoint in otlpEndpoints)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        if (enableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }
}
