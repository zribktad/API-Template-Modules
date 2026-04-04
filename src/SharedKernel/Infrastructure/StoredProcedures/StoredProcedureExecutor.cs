using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain.Interfaces;

namespace SharedKernel.Infrastructure.StoredProcedures;

/// <summary>
///     Generic EF Core implementation of <see cref="IStoredProcedureExecutor" /> backed by a module-specific
///     <see cref="DbContext" />.
/// </summary>
public sealed class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly DbContext _dbContext;

    public StoredProcedureExecutor(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TResult?> QueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class
    {
        return _dbContext
            .Set<TResult>()
            .FromSqlInterpolated(procedure.ToSql())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TResult>> QueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class
    {
        return await _dbContext
            .Set<TResult>()
            .FromSqlInterpolated(procedure.ToSql())
            .ToListAsync(ct);
    }

    public async Task<TResult?> ScalarFirstAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .Database.SqlQuery<TResult>(procedure.ToSql())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TResult>> ScalarManyAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
    {
        return await _dbContext.Database.SqlQuery<TResult>(procedure.ToSql()).ToListAsync(ct);
    }

    public Task<int> ExecuteAsync(FormattableString sql, CancellationToken ct = default)
    {
        return _dbContext.Database.ExecuteSqlInterpolatedAsync(sql, ct);
    }
}
