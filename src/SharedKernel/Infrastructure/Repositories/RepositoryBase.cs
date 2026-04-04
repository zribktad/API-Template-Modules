using System.Linq.Expressions;
using Ardalis.Specification;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Errors;
using SharedKernel.Domain.Common;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Repositories.Pagination;

namespace SharedKernel.Infrastructure.Repositories;

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
        IQueryable<T> baseQuery = ApplySpecification((ISpecification<T>)spec);
        IQueryable<T> countSource = ApplySpecification(spec, true);

        if (spec.Selector is null)
        {
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} must define a Select projection to use GetPagedAsync."
            );
        }

        Expression<Func<T, PagedRow<TResult>>> combinedSelector = spec.Selector.BuildPaged(
            countSource
        );
        int skip = (pageNumber - 1) * pageSize;
        List<PagedRow<TResult>> results = await baseQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(combinedSelector)
            .ToListAsync(ct);

        if (results.Count > 0)
        {
            return new PagedResponse<TResult>(
                results.Select(r => r.Item),
                results[0].TotalCount,
                pageNumber,
                pageSize
            );
        }

        if (pageNumber > 1)
        {
            int totalCount = await baseQuery.CountAsync(ct);
            if (totalCount > 0)
            {
                int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                return Error.Validation(
                    ErrorCatalog.General.PageOutOfRange,
                    $"PageNumber {pageNumber} exceeds total pages ({totalPages})."
                );
            }
        }

        return new PagedResponse<TResult>([], 0, pageNumber, pageSize);
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
