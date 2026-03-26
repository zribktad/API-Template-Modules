using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.RecurringJobRegistrations;

/// <summary>
/// Provides the <see cref="RecurringBackgroundJobDefinition"/> for the cleanup recurring job,
/// sourcing schedule and enablement from <see cref="BackgroundJobsOptions.Cleanup"/>.
/// </summary>
public sealed class CleanupRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    /// <summary>Builds the cleanup job definition from the supplied options.</summary>
    public RecurringBackgroundJobDefinition Build(BackgroundJobsOptions options) =>
        new(
            TickerQJobIds.Cleanup,
            TickerQFunctionNames.Cleanup,
            options.Cleanup.Cron,
            options.Cleanup.Enabled,
            "Runs invitation, soft-delete, and orphaned ProductData cleanup."
        );
}
