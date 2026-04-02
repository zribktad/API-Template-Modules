using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="FailedEmail"/> records, providing pessimistic-claim operations
/// used by the email retry background service to prevent duplicate processing.
/// </summary>
public interface IFailedEmailRepository
{
    /// <summary>Persists a new failed-email record to the store.</summary>
    Task AddAsync(FailedEmail failedEmail, CancellationToken ct = default);

    /// <summary>
    /// Atomically claims a batch of unclaimed, retryable emails (those below <paramref name="maxRetryAttempts"/>)
    /// and returns them for processing.
    /// </summary>
    Task<List<FailedEmail>> ClaimRetryableBatchAsync(
        int maxRetryAttempts,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    );

    /// <summary>
    /// Atomically claims a batch of emails whose claim lock has expired past <paramref name="cutoff"/>,
    /// allowing stale claims to be retried.
    /// </summary>
    Task<List<FailedEmail>> ClaimExpiredBatchAsync(
        DateTime cutoff,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    );

    /// <summary>Persists changes to an existing failed-email record (e.g. retry count increment or dead-letter flag).</summary>
    Task UpdateAsync(FailedEmail failedEmail, CancellationToken ct = default);

    /// <summary>Permanently removes a successfully processed failed-email record from the store.</summary>
    Task DeleteAsync(FailedEmail failedEmail, CancellationToken ct = default);
}
