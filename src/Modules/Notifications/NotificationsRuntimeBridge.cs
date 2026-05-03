using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Infrastructure.EFCore.Registration;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using BuildingBlocks.Web.Configuration;
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

        services.AddSingleton<IEmailTemplateRenderer, FluidEmailTemplateRenderer>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<IFailedEmailStore, FailedEmailStore>();
        services.AddSingleton<ISmtpSendPipelineProvider, SmtpSendPipelineProvider>();

        services.AddTransient<IEmailRetryService, EmailRetryService>();

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
