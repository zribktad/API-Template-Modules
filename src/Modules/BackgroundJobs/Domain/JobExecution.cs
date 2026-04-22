using ErrorOr;

namespace BackgroundJobs.Domain;

/// <summary>
///     Domain entity that tracks the lifecycle of a background job from submission through completion or failure.
///     Exposes domain methods to advance the job's <see cref="JobStatus" /> while keeping state transitions encapsulated.
/// </summary>
public sealed class JobExecution : IAuditableTenantEntity, IHasId
{
    public required string JobType { get; init; }
    public JobStatus Status { get; private set; } = JobStatus.Pending;
    public int ProgressPercent { get; private set; }
    public string? Parameters { get; init; }
    public string? CallbackUrl { get; init; }
    public string? ResultPayload { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime SubmittedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }

    public static JobExecution Create(
        string jobType,
        TimeProvider timeProvider,
        string? parameters = null,
        string? callbackUrl = null
    )
    {
        return new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = jobType,
            Parameters = parameters,
            CallbackUrl = callbackUrl,
            SubmittedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
    }

    /// <summary>
    ///     Transitions the job to <see cref="JobStatus.Processing" /> and records the start timestamp.
    ///     Only valid from <see cref="JobStatus.Pending" />.
    /// </summary>
    public ErrorOr<Success> MarkProcessing(TimeProvider timeProvider)
    {
        if (Status != JobStatus.Pending)
        {
            return Error.Conflict(
                code: "Job.InvalidTransition",
                description: $"Cannot transition to {nameof(JobStatus.Processing)} from {Status}."
            );
        }

        Status = JobStatus.Processing;
        StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success;
    }

    /// <summary>
    ///     Transitions the job to <see cref="JobStatus.Completed" />, sets progress to 100%, stores the optional result
    ///     payload, and records the completion timestamp.
    ///     Only valid from <see cref="JobStatus.Processing" />.
    /// </summary>
    public ErrorOr<Success> MarkCompleted(string? resultPayload, TimeProvider timeProvider)
    {
        if (Status != JobStatus.Processing)
        {
            return Error.Conflict(
                code: "Job.InvalidTransition",
                description: $"Cannot transition to {nameof(JobStatus.Completed)} from {Status}."
            );
        }

        Status = JobStatus.Completed;
        ProgressPercent = 100;
        ResultPayload = resultPayload;
        CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success;
    }

    /// <summary>
    ///     Transitions the job to <see cref="JobStatus.Failed" />, stores the error message, and records the completion
    ///     timestamp.
    ///     Only valid from <see cref="JobStatus.Processing" />.
    /// </summary>
    public ErrorOr<Success> MarkFailed(string errorMessage, TimeProvider timeProvider)
    {
        if (Status != JobStatus.Processing)
        {
            return Error.Conflict(
                code: "Job.InvalidTransition",
                description: $"Cannot transition to {nameof(JobStatus.Failed)} from {Status}."
            );
        }

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success;
    }

    /// <summary>
    ///     Updates the job's progress percentage, clamping the value to the valid range [0, 100].
    /// </summary>
    public void UpdateProgress(int percent)
    {
        ProgressPercent = Math.Clamp(percent, 0, 100);
    }
}
