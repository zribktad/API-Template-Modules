namespace APITemplate.Domain.Enums;

/// <summary>
/// Represents the execution state of a background <see cref="Entities.JobExecution"/>.
/// </summary>
public enum JobStatus
{
    /// <summary>The job has been submitted and is waiting to be picked up by a worker.</summary>
    Pending,

    /// <summary>A worker has claimed the job and is actively executing it.</summary>
    Processing,

    /// <summary>The job finished successfully.</summary>
    Completed,

    /// <summary>The job terminated with an error and will not be retried automatically.</summary>
    Failed,
}
