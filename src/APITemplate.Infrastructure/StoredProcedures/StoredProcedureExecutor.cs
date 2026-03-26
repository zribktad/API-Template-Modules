using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// EF Core implementation of <see cref="IStoredProcedureExecutor"/>.
///
/// All methods use <c>FromSql(FormattableString)</c> which converts each
/// interpolated argument into a named SQL parameter — SQL injection is impossible
/// as long as callers pass parameters via interpolation, never via concatenation.
///
/// The <c>DbContext.Set&lt;T&gt;()</c> approach is used instead of a typed DbSet property
/// so that adding a new stored procedure result type only requires:
///   1. A new keyless entity + HasNoKey() configuration
///   2. A new IStoredProcedure&lt;T&gt; implementation
///   No changes to AppDbContext or this executor are needed.
/// </summary>
public sealed class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly AppDbContext _dbContext;

    public StoredProcedureExecutor(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TResult?> QueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default)
        where TResult : class
        => StoredProcedureTelemetry.TraceQueryFirstAsync(
            procedure,
            () => _dbContext.Set<TResult>()
                .FromSql(procedure.ToSql())
                .FirstOrDefaultAsync(ct));

    public Task<IReadOnlyList<TResult>> QueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default)
        where TResult : class
        => StoredProcedureTelemetry.TraceQueryManyAsync(
            procedure,
            async () => await _dbContext.Set<TResult>()
                .FromSql(procedure.ToSql())
                .ToListAsync(ct));

    public Task<int> ExecuteAsync(FormattableString sql, CancellationToken ct = default)
        => StoredProcedureTelemetry.TraceExecuteAsync(
            sql,
            () => _dbContext.Database.ExecuteSqlAsync(sql, ct));
}
