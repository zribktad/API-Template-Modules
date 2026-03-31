namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Application-layer contract for retrying and dead-lettering failed outbound emails.
/// Implementations are driven by recurring background jobs in the Infrastructure layer.
/// </summary>
public interface IEmailRetryService
{
    /// <summary>
    /// Re-attempts delivery of previously failed emails up to <paramref name="maxRetryAttempts"/> times,
    /// processing up to <paramref name="batchSize"/> messages per invocation.
    /// </summary>
    Task RetryFailedEmailsAsync(
        int maxRetryAttempts,
        int batchSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Moves emails that have exceeded the <paramref name="deadLetterAfterHours"/> age threshold
    /// to a dead-letter store, processed in batches of <paramref name="batchSize"/>.
    /// </summary>
    Task DeadLetterExpiredAsync(
        int deadLetterAfterHours,
        int batchSize,
        CancellationToken ct = default
    );
}
