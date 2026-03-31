using APITemplate.Domain.Exceptions;
using Ardalis.Specification;
using ErrorOr;

namespace APITemplate.Application.Common.Extensions;

public static class RepositoryExtensions
{
    [Obsolete("Use GetByIdOrError with ErrorOr pattern instead.")]
    public static async Task<T> GetByIdOrThrowAsync<T>(
        this IRepositoryBase<T> repository,
        Guid id,
        string errorCode,
        CancellationToken ct = default
    )
        where T : class
    {
        return await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(typeof(T).Name, id, errorCode);
    }

    /// <summary>
    /// Returns the entity by <paramref name="id"/> wrapped in <see cref="ErrorOr{T}"/>,
    /// or the supplied <paramref name="notFoundError"/> when the entity does not exist.
    /// </summary>
    public static async Task<ErrorOr<T>> GetByIdOrError<T>(
        this IRepositoryBase<T> repository,
        Guid id,
        Error notFoundError,
        CancellationToken ct = default
    )
        where T : class
    {
        var entity = await repository.GetByIdAsync(id, ct);
        return entity is null ? notFoundError : entity;
    }
}
