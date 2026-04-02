using APITemplate.Domain.Exceptions;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories.Pagination;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// Base repository that wraps the Ardalis Specification EF Core repository, overriding write methods
/// to stage changes without flushing — persistence is deferred to <see cref="IUnitOfWork.CommitAsync"/>.
/// </summary>
// Generic base repository — T is constrained to class (reference type) so EF Core can track it.
// abstract = cannot be instantiated directly, must be inherited (e.g. ProductRepository : RepositoryBase<Product>).
// SaveChangesAsync is intentionally NOT called here — use IUnitOfWork.CommitAsync() in the service layer.
public abstract class RepositoryBase<T>
    : Ardalis.Specification.EntityFrameworkCore.RepositoryBase<T>,
        IRepository<T>
    where T : class
{
    // Cast to AppDbContext — Ardalis exposes DbContext as the base DbContext type
    protected AppDbContext AppDb => (AppDbContext)DbContext;

    protected RepositoryBase(AppDbContext dbContext)
        : base(dbContext) { }

    /// <summary>
    /// Returns a paged result where the total count is embedded as a scalar sub-query alongside
    /// the projected items. When the requested page is empty and <paramref name="pageNumber"/> &gt; 1,
    /// a second COUNT query is issued to determine whether the page is out of range.
    /// The <paramref name="spec"/> must contain filter, sort, and projection but <b>no</b> Skip/Take.
    /// </summary>
    public virtual async Task<PagedResponse<TResult>> GetPagedAsync<TResult>(
        ISpecification<T, TResult> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    )
    {
        // Get filtered + sorted entity query via virtual ApplySpecification
        // so derived repositories (e.g. TenantRepository) can customise the source queryable.
        var baseQuery = ApplySpecification((ISpecification<T>)spec);
        var countSource = ApplySpecification((ISpecification<T>)spec, evaluateCriteriaOnly: true);

        // Build combined projection: entity => new PagedRow(projection(entity), baseQuery.Count())
        if (spec.Selector is null)
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} must define a Select projection to use GetPagedAsync."
            );

        var combinedSelector = spec.Selector.BuildPaged(countSource);

        // Apply skip/take + combined select → single SQL query
        var skip = (pageNumber - 1) * pageSize;
        var results = await baseQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(combinedSelector)
            .ToListAsync(ct);

        // Unwrap
        if (results.Count > 0)
            return new PagedResponse<TResult>(
                results.Select(r => r.Item),
                results[0].TotalCount,
                pageNumber,
                pageSize
            );

        // Empty page — if pageNumber > 1, verify whether data actually exists
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

    // Override write methods — do NOT call SaveChangesAsync, that is UoW responsibility.
    // Return 0 (no rows persisted yet — UoW will commit later).
    /// <summary>Tracks <paramref name="entity"/> for insertion without flushing to the database.</summary>
    public override Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Add(entity);
        return Task.FromResult(entity);
    }

    /// <summary>Tracks multiple entities for insertion without flushing to the database.</summary>
    public override Task<IEnumerable<T>> AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().AddRange(entities);
        return Task.FromResult(entities);
    }

    /// <summary>Marks <paramref name="entity"/> as modified without flushing to the database.</summary>
    public override Task<int> UpdateAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Update(entity);
        return Task.FromResult(0);
    }

    /// <summary>Marks multiple entities as modified without flushing to the database.</summary>
    public override Task<int> UpdateRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().UpdateRange(entities);
        return Task.FromResult(0);
    }

    /// <summary>Marks <paramref name="entity"/> for deletion without flushing to the database.</summary>
    public override Task<int> DeleteAsync(T entity, CancellationToken ct = default)
    {
        DbContext.Set<T>().Remove(entity);
        return Task.FromResult(0);
    }

    /// <summary>Marks multiple entities for deletion without flushing to the database.</summary>
    public override Task<int> DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken ct = default
    )
    {
        DbContext.Set<T>().RemoveRange(entities);
        return Task.FromResult(0);
    }

    // Guid-based delete (our contract, not in IRepositoryBase)
    /// <summary>
    /// Looks up the entity by <paramref name="id"/> and marks it for deletion.
    /// Throws <see cref="NotFoundException"/> when the entity does not exist.
    /// </summary>
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
