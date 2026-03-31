using APITemplate.Application.Common.Options;

namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Marker interface that each recurring background job implements to self-describe its schedule.
/// The Infrastructure bootstrapper discovers all registrations via DI and registers them with the scheduler.
/// </summary>
public interface IRecurringBackgroundJobRegistration
{
    /// <summary>
    /// Produces the <see cref="RecurringBackgroundJobDefinition"/> for this job using values
    /// from <paramref name="options"/> (e.g. cron expressions, retry counts).
    /// </summary>
    RecurringBackgroundJobDefinition Build(BackgroundJobsOptions options);
}
