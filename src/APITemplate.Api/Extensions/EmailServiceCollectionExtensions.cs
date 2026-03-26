using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Infrastructure.Email;
using APITemplate.Infrastructure.Security;
using Polly;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers email infrastructure: SMTP sender,
/// Fluid template renderer, channel-based queue with background consumer, and a Polly
/// exponential-backoff resilience pipeline for delivery retries.
/// </summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers email services including the MailKit SMTP sender, Fluid template renderer,
    /// failed-email store, and a Polly retry pipeline keyed to <see cref="ResiliencePipelineKeys.SmtpSend"/>.
    /// </summary>
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var emailSection = configuration.SectionFor<EmailOptions>();
        var emailOptions = emailSection.Get<EmailOptions>() ?? new EmailOptions();
        services.Configure<EmailOptions>(emailSection);

        services.AddQueueWithConsumer<
            ChannelEmailQueue,
            IEmailQueue,
            IEmailQueueReader,
            EmailSendingBackgroundService
        >();
        services.AddSingleton<IEmailTemplateRenderer, FluidEmailTemplateRenderer>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddTransient<IEmailSender, MailKitEmailSender>();

        services.AddSingleton<IFailedEmailStore, FailedEmailStore>();

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.SmtpSend,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = emailOptions.MaxRetryAttempts,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(emailOptions.RetryBaseDelaySeconds),
                        UseJitter = true,
                    }
                );
            }
        );

        return services;
    }
}
