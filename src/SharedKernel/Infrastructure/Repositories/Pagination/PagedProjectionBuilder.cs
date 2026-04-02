using System.Linq.Expressions;

namespace SharedKernel.Infrastructure.Repositories.Pagination;

/// <summary>
/// Composes an existing projection expression with a scalar COUNT sub-query
/// so that EF Core can retrieve both the projected items and the total count
/// in a single SQL round-trip.
/// </summary>
internal static class PagedProjectionBuilder
{
    internal static Expression<Func<T, PagedRow<TResult>>> BuildPaged<T, TResult>(
        this Expression<Func<T, TResult>> selector,
        IQueryable<T> countSource
    )
    {
        var countCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Count),
            [typeof(T)],
            countSource.Expression
        );

        var ctor =
            typeof(PagedRow<TResult>).GetConstructor([typeof(TResult), typeof(int)])
            ?? throw new InvalidOperationException(
                $"No suitable constructor found for {typeof(PagedRow<TResult>)} with parameters (TResult, int)."
            );

        var newExpr = Expression.New(ctor, selector.Body, countCall);
        var entityParam = selector.Parameters[0];
        return Expression.Lambda<Func<T, PagedRow<TResult>>>(newExpr, entityParam);
    }
}
