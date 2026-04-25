using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class ExternalSyncRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    private readonly IOptions<BackgroundJobsOptions> _options;

    public ExternalSyncRecurringJobRegistration(IOptions<BackgroundJobsOptions> options)
    {
        _options = options;
    }

    public RecurringBackgroundJobDefinition Build()
    {
        BackgroundJobsOptions options = _options.Value;
        return new RecurringBackgroundJobDefinition(
            TickerQJobIds.ExternalSync,
            TickerQFunctionNames.ExternalSync,
            options.ExternalSync.Cron,
            options.ExternalSync.Enabled,
            "Runs periodic synchronization for configured external integrations."
        );
    }
}
