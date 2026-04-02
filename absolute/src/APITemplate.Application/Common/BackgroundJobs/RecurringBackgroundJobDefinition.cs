namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Immutable descriptor for a recurring background job passed from the Application layer to the
/// Infrastructure scheduler (e.g. Hangfire). Each <see cref="IRecurringBackgroundJobRegistration"/>
/// produces one instance of this record.
/// </summary>
/// <param name="Id">Stable identifier for the job, used to upsert the schedule in the scheduler.</param>
/// <param name="FunctionName">The scheduler entry-point function name (e.g. Hangfire job method name).</param>
/// <param name="CronExpression">Cron expression that controls the execution frequency.</param>
/// <param name="Enabled">When <c>false</c> the scheduler should skip or remove this job without error.</param>
/// <param name="Description">Human-readable description shown in the scheduler dashboard.</param>
/// <param name="Retries">Number of automatic retry attempts on failure.</param>
/// <param name="RetryIntervals">Optional delay intervals (in seconds) between consecutive retry attempts.</param>
public sealed record RecurringBackgroundJobDefinition(
    Guid Id,
    string FunctionName,
    string CronExpression,
    bool Enabled,
    string Description,
    int Retries = 0,
    int[]? RetryIntervals = null
) : IHasId;
