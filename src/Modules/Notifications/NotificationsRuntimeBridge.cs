using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Notifications.Domain;
using Notifications.Persistence;
using Notifications.Repositories;
using Notifications.Services;
using Polly;
using Polly.Retry;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Startup;

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
            .ForwardUnitOfWork<NotificationsDbMarker>()
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

        services.AddSingleton<
            IRecurringBackgroundJobRegistration,
            EmailRetryRecurringJobRegistration
        >();

        services.AddSingleton<
            IDatabaseStartupContributor,
            NotificationsDatabaseStartupContributor
        >();

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.SmtpSend,
            builder =>
            {
                builder.AddRetry(
                    new RetryStrategyOptions
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
