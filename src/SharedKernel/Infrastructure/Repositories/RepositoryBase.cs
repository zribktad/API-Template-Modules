using SharedKernel.Application.Errors;
using SharedKernel.Domain.Common;
using SharedKernel.Domain.Exceptions;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Repositories.Pagination;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.Repositories;

/// <summary>
/// Generic EF Core repository that stages changes without flushing and provides shared paged-query support.
/// </summary>
public abstract class RepositoryBase<T> :
    Ardalis.Specification.EntityFrameworkCore.RepositoryBase<T>,
    IRepository<T>
    where T : class
{
    protected RepositoryBase(DbContext dbContext)
        : base(dbContext) { }

    public virtual async Task<PagedResponse<TResult>> GetPagedAsync<TResult>(
        ISpecification<T, TResult> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var baseQuery = ApplySpecification((ISpecification<T>)spec);
        var countSource = ApplySpecification((ISpecification<T>)spec, evaluateCriteriaOnly: true);

        if (spec.Selector is null)
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} must define a Select projection to use GetPagedAsync."
            );

        var combinedSelector = spec.Selector.BuildPaged(countSource);
        var skip = (pageNumber - 1) * pageSize;
        var results = await baseQuery
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
            var totalCount = await baseQuery.CountAsync(ct);
            if (totalCount > 0)
            {
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                throw new ValidationException(
                    $"PageNumber {pageNumber} exceeds total pages ({totalPages}).",
                    ErrorCatalog.General.PageOutOfRange
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

    [Obsolete("Use GetByIdAsync + DeleteAsync(entity) with ErrorOr pattern instead.")]
    public async Task DeleteAsync(Guid id, CancellationToken ct = default, string? errorCode = null)
    {
        var entity =
            await GetByIdAsync(id, ct)
            ?? throw new NotFoundException(
                typeof(T).Name,
                id,
                errorCode ?? ErrorCatalog.General.NotFound
            );
        DbContext.Set<T>().Remove(entity);
    }
}
