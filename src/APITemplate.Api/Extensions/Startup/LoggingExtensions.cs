using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using SharedKernel.Application.Options.Security;
using SharedKernel.Infrastructure.Logging;

namespace APITemplate.Api.Extensions.Startup;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedOptions<RedactionOptions>(builder.Configuration);

        builder.Services.AddRedaction(redactionBuilder =>
        {
            redactionBuilder.SetRedactor<ErasingRedactor>(LogDataClassifications.Personal);

#pragma warning disable EXTEXP0002 // HMAC redactor is experimental in Microsoft.Extensions.Compliance.Redaction.
            redactionBuilder.SetHmacRedactor(
                options =>
                {
                    RedactionOptions redactionOptions =
                        builder.Configuration.SectionFor<RedactionOptions>().Get<RedactionOptions>()
                        ?? throw new InvalidOperationException(
                            $"Configuration section '{nameof(RedactionOptions)}' is missing."
                        );

                    string hmacKey = RedactionConfiguration.ResolveHmacKey(
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
}
