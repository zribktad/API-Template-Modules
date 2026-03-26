using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.RecurringJobRegistrations;

/// <summary>
/// Provides the <see cref="RecurringBackgroundJobDefinition"/> for the external-sync recurring job,
/// sourcing schedule and enablement from <see cref="BackgroundJobsOptions.ExternalSync"/>.
/// </summary>
public sealed class ExternalSyncRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    /// <summary>Builds the external-sync job definition from the supplied options.</summary>
    public RecurringBackgroundJobDefinition Build(BackgroundJobsOptions options) =>
        new(
            TickerQJobIds.ExternalSync,
            TickerQFunctionNames.ExternalSync,
            options.ExternalSync.Cron,
            options.ExternalSync.Enabled,
            "Runs periodic synchronization for configured external integrations."
        );
}
