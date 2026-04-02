using System.ComponentModel.DataAnnotations;
using APITemplate.Infrastructure.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace APITemplate.Api.Extensions.Startup;

/// <summary>
/// Presentation-layer extension class that configures Microsoft.Extensions.Compliance
/// redaction (HMAC for sensitive data, erasure for personal data) and Serilog OpenTelemetry sinks.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Registers HMAC and erasing redactors for sensitive and personal data classifications
    /// and enables log redaction on the host's logging pipeline.
    /// </summary>
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        builder.Services.AddRedaction(redactionBuilder =>
        {
            redactionBuilder.SetRedactor<ErasingRedactor>(LogDataClassifications.Personal);

#pragma warning disable EXTEXP0002 // HMAC redactor API is currently marked experimental in the Microsoft.Extensions.Compliance.Redaction package.
            redactionBuilder.SetHmacRedactor(
                options =>
                {
                    var redactionOptions =
                        builder.Configuration.SectionFor<RedactionOptions>().Get<RedactionOptions>()
                        ?? new RedactionOptions();
                    Validator.ValidateObject(
                        redactionOptions,
                        new ValidationContext(redactionOptions),
                        validateAllProperties: true
                    );

                    var hmacKey = RedactionConfiguration.ResolveHmacKey(
                        redactionOptions,
                        Environment.GetEnvironmentVariable
                    );

                    options.KeyId = redactionOptions.KeyId;
                    options.Key = hmacKey;
                },
                new DataClassificationSet(LogDataClassifications.Sensitive)
            );
#pragma warning restore EXTEXP0002

            redactionBuilder.SetFallbackRedactor<ErasingRedactor>();
        });

        builder.Logging.EnableRedaction();

        return builder;
    }

    /// <summary>
    /// Attaches Serilog OpenTelemetry sinks for each enabled OTLP endpoint, enriching log
    /// events with activity trace/span IDs and OpenTelemetry resource attributes.
    /// </summary>
    public static LoggerConfiguration AddOpenTelemetrySinks(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        loggerConfiguration.Enrich.FromLogContext().Enrich.With<ActivityTraceEnricher>();

        var options = ObservabilityServiceCollectionExtensions.GetObservabilityOptions(
            configuration
        );

        var appOptions = ObservabilityServiceCollectionExtensions.GetAppOptions(configuration);

        var resourceAttributes = ObservabilityServiceCollectionExtensions.BuildResourceAttributes(
            appOptions,
            environment
        );
        var endpoints = ObservabilityServiceCollectionExtensions.GetEnabledOtlpEndpoints(
            options,
            environment
        );

        foreach (var endpoint in endpoints)
        {
            loggerConfiguration.WriteTo.OpenTelemetry(otel =>
            {
                otel.Endpoint = endpoint;
                otel.Protocol = OtlpProtocol.Grpc;
                otel.IncludedData =
                    IncludedData.MessageTemplateTextAttribute
                    | IncludedData.SpecRequiredResourceAttributes
                    | IncludedData.TraceIdField
                    | IncludedData.SpanIdField;
                otel.ResourceAttributes = resourceAttributes;
            });
        }

        return loggerConfiguration;
    }
}
