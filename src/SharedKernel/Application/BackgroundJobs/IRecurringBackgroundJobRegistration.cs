namespace SharedKernel.Application.BackgroundJobs;

/// <summary>
///     A contract implemented per-job to provide the metadata and scheduling intent required
///     by the TickerQ scheduler database.
/// </summary>
public interface IRecurringBackgroundJobRegistration
{
    /// <summary>
    ///     Constructs the raw <see cref="RecurringBackgroundJobDefinition" /> struct to be persisted.
    ///     Typically reads the required interval and enablement from configuration bound elsewhere.
    /// </summary>
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider);
}
