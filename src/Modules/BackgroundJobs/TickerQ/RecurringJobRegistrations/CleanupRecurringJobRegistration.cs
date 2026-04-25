using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class CleanupRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    private readonly IOptions<BackgroundJobsOptions> _options;

    public CleanupRecurringJobRegistration(IOptions<BackgroundJobsOptions> options)
    {
        _options = options;
    }

    public RecurringBackgroundJobDefinition Build()
    {
        BackgroundJobsOptions options = _options.Value;
        return new RecurringBackgroundJobDefinition(
            TickerQJobIds.Cleanup,
            TickerQFunctionNames.Cleanup,
            options.Cleanup.Cron,
            options.Cleanup.Enabled,
            "Runs invitation, soft-delete, and orphaned ProductData cleanup."
        );
    }
}
