namespace BackgroundJobs.Domain;

/// <summary>
/// Provides mapping utilities between <see cref="JobExecution"/> domain entities and <see cref="JobStatusResponse"/> DTOs.
/// </summary>
public static class JobMappings
{
    /// <summary>Maps a <see cref="JobExecution"/> to a <see cref="JobStatusResponse"/>.</summary>
    public static JobStatusResponse ToResponse(this JobExecution entity) =>
        new(
            entity.Id,
            entity.JobType,
            entity.Status,
            entity.ProgressPercent,
            entity.Parameters,
            entity.ResultPayload,
            entity.ErrorMessage,
            entity.SubmittedAtUtc,
            entity.StartedAtUtc,
            entity.CompletedAtUtc,
            entity.CallbackUrl
        );
}





