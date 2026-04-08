using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options;

namespace Notifications.Services;

/// <summary>
///     Provides the <see cref="RecurringBackgroundJobDefinition" /> for the email-retry recurring job,
///     sourcing schedule and enablement from <see cref="EmailRetryJobOptions" />.
/// </summary>
public sealed class EmailRetryRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    /// <summary>Builds the email-retry job definition from the supplied options.</summary>
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        EmailRetryJobOptions options = serviceProvider
            .GetRequiredService<IOptions<EmailRetryJobOptions>>()
            .Value;
        return new RecurringBackgroundJobDefinition(
            new Guid("31261201-e220-45d0-bd7e-6d662ca1acaf"),
            "email-retry-recurring-job",
            options.Cron,
            options.Enabled,
            "Retries failed emails and dead-letters expired retry records."
        );
    }
}
