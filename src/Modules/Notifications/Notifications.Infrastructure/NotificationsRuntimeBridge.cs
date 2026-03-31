using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Common.Email;
using Notifications.Domain;
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
        string connectionString = configuration.GetConnectionString(ConfigurationSections.DefaultConnection)!;

        // Common resilient configuration for email and database
        services
            .AddModule<NotificationsDbContext>(configuration)
            .ConfigureDbContext((_, options) => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .AddRepository<IFailedEmailRepository, FailedEmailRepository>();

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
        // Using it directly here because it was coupled to emails before. Identity might recreate or share.
        services.AddSingleton<ISecureTokenGenerator, Notifications.Infrastructure.Security.SecureTokenGenerator>(); 
        services.AddTransient<IEmailSender, MailKitEmailSender>();
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
                        BackoffType = DelayBackoffType.Exponential, // fixed below
                        Delay = TimeSpan.FromSeconds(emailOptions.RetryBaseDelaySeconds),
                        UseJitter = true,
                    }
                );
            }
        );

        return services;
    }
}
