using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="FailedEmail"/> that coordinates direct context access
/// with stored-procedure-based batch claiming for concurrent retry processing.
/// </summary>
public sealed class FailedEmailRepository : IFailedEmailRepository
{
    private readonly AppDbContext _dbContext;
    private readonly IStoredProcedureExecutor _executor;

    public FailedEmailRepository(AppDbContext dbContext, IStoredProcedureExecutor executor)
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

    /// <summary>Atomically claims a batch of retryable failed emails via the <see cref="ClaimRetryableFailedEmailsProcedure"/> stored procedure.</summary>
    public async Task<List<FailedEmail>> ClaimRetryableBatchAsync(
        int maxRetryAttempts,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    )
    {
        var procedure = new ClaimRetryableFailedEmailsProcedure(
            maxRetryAttempts,
            batchSize,
            claimedBy,
            claimedAtUtc,
            claimedUntilUtc
        );
        var result = await _executor.QueryManyAsync(procedure, ct);
        return result.ToList();
    }

    /// <summary>Atomically claims a batch of expired (dead-letter candidate) failed emails via the <see cref="ClaimExpiredFailedEmailsProcedure"/> stored procedure.</summary>
    public async Task<List<FailedEmail>> ClaimExpiredBatchAsync(
        DateTime cutoff,
        int batchSize,
        string claimedBy,
        DateTime claimedAtUtc,
        DateTime claimedUntilUtc,
        CancellationToken ct = default
    )
    {
        var procedure = new ClaimExpiredFailedEmailsProcedure(
            cutoff,
            batchSize,
            claimedBy,
            claimedAtUtc,
            claimedUntilUtc
        );
        var result = await _executor.QueryManyAsync(procedure, ct);
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
}
