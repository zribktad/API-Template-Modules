using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Common.BackgroundJobs;
using Notifications.Application.Common.Email;
using Notifications.Domain;
using Notifications.Infrastructure.BackgroundJobs.Services;
using Notifications.Infrastructure.Email;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Repositories;
using Polly;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;

namespace Notifications;

public static class NotificationsRuntimeBridge
{
    public static IServiceCollection AddNotificationsRuntimeBridge(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        // Common resilient configuration for email and database
        services
            .AddModule<NotificationsDbContext>(configuration)
            .ConfigureDbContext(options => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<Notifications.Domain.NotificationsDbMarker>()
            .AddRepository<IFailedEmailRepository, FailedEmailRepository>();

        IConfigurationSection emailSection = configuration.SectionFor<EmailOptions>();
        EmailOptions emailOptions = emailSection.Get<EmailOptions>() ?? new EmailOptions();
        services.AddValidatedOptions<EmailOptions>(configuration);

        services.AddQueueWithConsumer<
            ChannelEmailQueue,
            IEmailQueue,
            IEmailQueueReader,
            EmailSendingBackgroundService
        >();

        services.AddSingleton<IEmailTemplateRenderer, FluidEmailTemplateRenderer>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<IFailedEmailStore, FailedEmailStore>();

        services.AddTransient<IEmailRetryService, EmailRetryService>();

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
