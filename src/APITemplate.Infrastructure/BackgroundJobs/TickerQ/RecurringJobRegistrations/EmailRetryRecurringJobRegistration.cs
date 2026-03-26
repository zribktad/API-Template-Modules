using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.RecurringJobRegistrations;

/// <summary>
/// Provides the <see cref="RecurringBackgroundJobDefinition"/> for the email-retry recurring job,
/// sourcing schedule and enablement from <see cref="BackgroundJobsOptions.EmailRetry"/>.
/// </summary>
public sealed class EmailRetryRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    /// <summary>Builds the email-retry job definition from the supplied options.</summary>
    public RecurringBackgroundJobDefinition Build(BackgroundJobsOptions options) =>
        new(
            TickerQJobIds.EmailRetry,
            TickerQFunctionNames.EmailRetry,
            options.EmailRetry.Cron,
            options.EmailRetry.Enabled,
            "Retries failed emails and dead-letters expired retry records."
        );
}
