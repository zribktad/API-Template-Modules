using BuildingBlocks.Application.Options.Security;
using BuildingBlocks.Web.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace APITemplate.Api.Extensions.Startup;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        AddApplicationRedaction(builder.Services, builder.Configuration);
        builder.Logging.EnableRedaction();
        return builder;
    }

    public static IServiceCollection AddApplicationRedaction(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RedactionOptions>(configuration);

        services.AddRedaction(redactionBuilder =>
        {
#pragma warning disable EXTEXP0002 // HMAC redactor is experimental in Microsoft.Extensions.Compliance.Redaction.
            void ConfigureHmac(HmacRedactorOptions options)
            {
                RedactionOptions redactionOptions =
                    configuration.SectionFor<RedactionOptions>().Get<RedactionOptions>()
                    ?? throw new InvalidOperationException(
                        $"Configuration section '{nameof(RedactionOptions)}' is missing."
                    );

                string hmacKey = RedactionConfiguration.ResolveHmacKey(
                    redactionOptions,
                    Environment.GetEnvironmentVariable
                );

                options.KeyId = redactionOptions.KeyId;
                options.Key = hmacKey;
            }

            redactionBuilder.SetHmacRedactor(ConfigureHmac, LogDataClassifications.Personal);
            redactionBuilder.SetHmacRedactor(ConfigureHmac, LogDataClassifications.Sensitive);
#pragma warning restore EXTEXP0002

            redactionBuilder.SetFallbackRedactor<ErasingRedactor>();
        });

        return services;
    }
}
