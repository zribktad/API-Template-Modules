using Notifications.Application.Common.BackgroundJobs;


namespace Notifications.Infrastructure.Email;

/// <summary>
/// Provides the <see cref="RecurringBackgroundJobDefinition"/> for the email-retry recurring job,
/// sourcing schedule and enablement from <see cref="BackgroundJobsOptions.EmailRetry"/>.
/// </summary>
public sealed class EmailRetryRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    /// <summary>Builds the email-retry job definition from the supplied options.</summary>
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
        return new(
            new Guid("31261201-e220-45d0-bd7e-6d662ca1acaf"),
            "email-retry-recurring-job",
            options.RetryCron,
            options.RetryEnabled,
            "Retries failed emails and dead-letters expired retry records."
        );
    }
}
