using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class ReindexRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    private readonly IOptions<BackgroundJobsOptions> _options;

    public ReindexRecurringJobRegistration(IOptions<BackgroundJobsOptions> options)
    {
        _options = options;
    }

    public RecurringBackgroundJobDefinition Build()
    {
        BackgroundJobsOptions options = _options.Value;
        return new RecurringBackgroundJobDefinition(
            TickerQJobIds.Reindex,
            TickerQFunctionNames.Reindex,
            options.Reindex.Cron,
            options.Reindex.Enabled,
            "Rebuilds the PostgreSQL full-text search indexes."
        );
    }
}
