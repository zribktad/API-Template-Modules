using APITemplate.Domain.Enums;

namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity that tracks the lifecycle of a background job from submission through completion or failure.
/// Exposes domain methods to advance the job's <see cref="JobStatus"/> while keeping state transitions encapsulated.
/// </summary>
public sealed class JobExecution : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }
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

    /// <summary>
    /// Transitions the job to <see cref="JobStatus.Processing"/> and records the start timestamp.
    /// </summary>
    public void MarkProcessing(TimeProvider timeProvider)
    {
        Status = JobStatus.Processing;
        StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Transitions the job to <see cref="JobStatus.Completed"/>, sets progress to 100%, stores the optional result payload, and records the completion timestamp.
    /// </summary>
    public void MarkCompleted(string? resultPayload, TimeProvider timeProvider)
    {
        Status = JobStatus.Completed;
        ProgressPercent = 100;
        ResultPayload = resultPayload;
        CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Transitions the job to <see cref="JobStatus.Failed"/>, stores the error message, and records the completion timestamp.
    /// </summary>
    public void MarkFailed(string errorMessage, TimeProvider timeProvider)
    {
        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Updates the job's progress percentage, clamping the value to the valid range [0, 100].
    /// </summary>
    public void UpdateProgress(int percent)
    {
        ProgressPercent = Math.Clamp(percent, 0, 100);
    }
}
