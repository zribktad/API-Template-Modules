namespace BackgroundJobs.Domain;

/// <summary>
///     Application-layer contract for scheduled email-retry operations.
///     Implementations live in the Infrastructure layer and delegate to the Notifications module
///     via Wolverine commands. Invoked by the recurring email-retry background job.
/// </summary>
public interface IEmailRetryJobService
{
    /// <summary>
    ///     Dispatches a command to re-attempt delivery of previously failed emails.
    /// </summary>
    Task RetryFailedEmailsAsync(CancellationToken ct = default);

    /// <summary>
    ///     Dispatches a command to move emails that have exceeded the age threshold
    ///     to a dead-letter store.
    /// </summary>
    Task DeadLetterExpiredAsync(CancellationToken ct = default);
}
