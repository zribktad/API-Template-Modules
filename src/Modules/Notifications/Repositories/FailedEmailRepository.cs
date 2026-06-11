using BuildingBlocks.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Persistence;
using Notifications.StoredProcedures;

namespace Notifications.Repositories;

/// <summary>
///     EF Core repository for <see cref="FailedEmail" /> that coordinates direct context access
///     with stored-procedure-based batch claiming for concurrent retry processing.
/// </summary>
public sealed class FailedEmailRepository : IFailedEmailRepository
{
    private readonly NotificationsDbContext _dbContext;
    private readonly IStoredProcedureExecutor<NotificationsDbMarker> _executor;

    public FailedEmailRepository(
        NotificationsDbContext dbContext,
        IStoredProcedureExecutor<NotificationsDbMarker> executor
    )
    {
        _dbContext = dbContext;
        _executor = executor;
    }

    /// <summary>Stages the failed email for insertion without flushing to the database.</summary>
    public Task AddAsync(FailedEmail failedEmail, CancellationToken ct = default)
    {
        _dbContext.FailedEmails.Add(failedEmail);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Atomically claims a batch of retryable failed emails via the
    ///     <see cref="ClaimRetryableFailedEmailsProcedure" /> stored procedure.
    /// </summary>
    public async Task<List<FailedEmail>> ClaimRetryableBatchAsync(
        int maxRetryAttempts,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    )
    {
        ClaimRetryableFailedEmailsProcedure procedure = new(
            maxRetryAttempts,
            batchSize,
            claimedBy,
            claimedAtUtc,
            claimedUntilUtc
        );
        IReadOnlyList<FailedEmail> result = await _executor.QueryManyAsync(procedure, ct);
        return result.ToList();
    }

    /// <summary>
    ///     Atomically claims a batch of expired (dead-letter candidate) failed emails via the
    ///     <see cref="ClaimExpiredFailedEmailsProcedure" /> stored procedure.
    /// </summary>
    public async Task<List<FailedEmail>> ClaimExpiredBatchAsync(
        DateTime cutoff,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    )
    {
        ClaimExpiredFailedEmailsProcedure procedure = new(
            cutoff,
            batchSize,
            claimedBy,
            claimedAtUtc,
            claimedUntilUtc
        );
        IReadOnlyList<FailedEmail> result = await _executor.QueryManyAsync(procedure, ct);
        return result.ToList();
    }

    /// <summary>Stages an update for the failed email without flushing to the database.</summary>
    public Task UpdateAsync(FailedEmail failedEmail, CancellationToken ct = default)
    {
        _dbContext.FailedEmails.Update(failedEmail);
        return Task.CompletedTask;
    }

    /// <summary>Stages a hard delete (physical removal) for the failed email without flushing to the database.</summary>
    public Task DeleteAsync(FailedEmail failedEmail, CancellationToken ct = default)
    {
        _dbContext.FailedEmails.Remove(failedEmail);
        return Task.CompletedTask;
    }

    public async Task<int> DeleteByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.FailedEmails.Where(e => e.Id == id).ExecuteDeleteAsync(ct);

    public async Task<FailedEmail?> FindTrackedByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.FailedEmails.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.FailedEmails.AsNoTracking().AnyAsync(e => e.Id == id, ct);

    public void ClearChangeTracker() => _dbContext.ChangeTracker.Clear();
}
