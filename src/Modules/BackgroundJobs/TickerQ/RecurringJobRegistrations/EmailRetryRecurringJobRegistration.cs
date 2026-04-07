using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class EmailRetryRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        EmailRetryJobOptions options = serviceProvider
            .GetRequiredService<IOptions<BackgroundJobsOptions>>()
            .Value.EmailRetry;

        return new RecurringBackgroundJobDefinition(
            new Guid("31261201-e220-45d0-bd7e-6d662ca1acaf"),
            "email-retry-recurring-job",
            options.Cron,
            options.Enabled,
            "Retries failed emails and dead-letters expired retry records."
        );
    }
}