using SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.StoredProcedures;

/// <summary>
/// Generic EF Core implementation of <see cref="IStoredProcedureExecutor"/> backed by a module-specific <see cref="DbContext"/>.
/// </summary>
public sealed class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly DbContext _dbContext;

    public StoredProcedureExecutor(DbContext dbContext) => _dbContext = dbContext;

    public Task<TResult?> QueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class =>
        _dbContext.Set<TResult>().FromSql(procedure.ToSql()).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TResult>> QueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class =>
        await _dbContext.Set<TResult>().FromSql(procedure.ToSql()).ToListAsync(ct);

    public Task<int> ExecuteAsync(FormattableString sql, CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlAsync(sql, ct);
}
