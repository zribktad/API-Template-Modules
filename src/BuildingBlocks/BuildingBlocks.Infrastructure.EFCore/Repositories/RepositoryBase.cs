using Ardalis.Specification;
using BuildingBlocks.Domain.Common;
using BuildingBlocks.Domain.Interfaces;
using BuildingBlocks.Infrastructure.EFCore.Repositories.Pagination;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.EFCore.Repositories;

/// <summary>
///     Generic EF Core repository that stages changes without flushing and provides shared paged-query support.
/// </summary>
public abstract class RepositoryBase<T>
    : Ardalis.Specification.EntityFrameworkCore.RepositoryBase<T>,
        IRepository<T>
    where T : class
{
    protected RepositoryBase(DbContext dbContext)
        : base(dbContext) { }

    public virtual async Task<ErrorOr<PagedResponse<TResult>>> GetPagedAsync<TResult>(
        ISpecification<T, TResult> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        if (spec.Selector is null)
        {
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} must define a Select projection to use GetPagedAsync."
            );
        }

        IQueryable<T> baseQuery = ApplySpecification((ISpecification<T>)spec);
        IQueryable<T> countSource = ApplySpecification(spec, true);

        return await PagedQueryExecutor.ExecuteAsync(
            baseQuery,
            countSource,
            spec.Selector,
            pageNumber,
            pageSize,
            ct
        );
    }

    public override Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Add(entity);
        return Task.FromResult(entity);
    }

    public override Task<IEnumerable<T>> AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().AddRange(entities);
        return Task.FromResult(entities);
    }

    public override Task<int> UpdateAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Update(entity);
        return Task.FromResult(0);
    }

    public override Task<int> UpdateRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().UpdateRange(entities);
        return Task.FromResult(0);
    }

    public override Task<int> DeleteAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Remove(entity);
        return Task.FromResult(0);
    }

    public override Task<int> DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().RemoveRange(entities);
        return Task.FromResult(0);
    }
}
