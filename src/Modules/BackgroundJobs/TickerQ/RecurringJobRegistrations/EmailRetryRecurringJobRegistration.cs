using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class EmailRetryRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    private readonly IOptions<BackgroundJobsOptions> _options;

    public EmailRetryRecurringJobRegistration(IOptions<BackgroundJobsOptions> options)
    {
        _options = options;
    }

    public RecurringBackgroundJobDefinition Build()
    {
        EmailRetryJobOptions options = _options.Value.EmailRetry;

        return new RecurringBackgroundJobDefinition(
            new Guid("31261201-e220-45d0-bd7e-6d662ca1acaf"),
            "email-retry-recurring-job",
            options.Cron,
            options.Enabled,
            "Retries failed emails and dead-letters expired retry records."
        );
    }
}
