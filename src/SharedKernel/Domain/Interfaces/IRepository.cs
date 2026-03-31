using Ardalis.Specification;
using SharedKernel.Domain.Common;

namespace SharedKernel.Domain.Interfaces;

/// <summary>
/// Generic repository abstraction that extends Ardalis <see cref="IRepositoryBase{T}"/> with an additional
/// delete-by-ID overload, providing a consistent data-access contract for all relational domain entities.
/// </summary>
public interface IRepository<T> : IRepositoryBase<T>
    where T : class
{
    // Inherited from IRepositoryBase<T> (Ardalis):
    //   GetByIdAsync<TId>(TId id, ct)
    //   ListAsync(ISpecification<T>, ct) → List<T>
    //   ListAsync(ISpecification<T, TResult>, ct) → List<TResult>   ← HTTP path
    //   FirstOrDefaultAsync, CountAsync, AnyAsync, ...
    //   AddAsync(T entity, ct), UpdateAsync(T entity, ct), DeleteAsync(T entity, ct)

    /// <summary>
    /// Returns a single-query paged result by embedding the total count as a scalar sub-query,
    /// eliminating the need for a separate COUNT query.
    /// The specification must contain filter, sort, and projection but <b>no</b> Skip/Take.
    /// </summary>
    Task<PagedResponse<TResult>> GetPagedAsync<TResult>(
        ISpecification<T, TResult> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default
    );

    // Ardalis only has DeleteAsync(T entity), we also need DeleteAsync(Guid id)
    /// <summary>
    /// Deletes the entity with the given <paramref name="id"/>; throws <see cref="Exceptions.NotFoundException"/> when no entity is found.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default, string? errorCode = null);
}
