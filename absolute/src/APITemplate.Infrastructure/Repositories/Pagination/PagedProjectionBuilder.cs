using System.Linq.Expressions;

namespace APITemplate.Infrastructure.Repositories.Pagination;

/// <summary>
/// Composes an existing projection expression with a scalar COUNT sub-query
/// so that EF Core can retrieve both the projected items and the total count
/// in a single SQL round-trip.
/// </summary>
internal static class PagedProjectionBuilder
{
    /// <summary>
    /// Builds <c>entity =&gt; new PagedRow&lt;TResult&gt;(selector(entity), countSource.Count())</c>
    /// as an expression tree that EF Core translates into a scalar sub-query for the count.
    /// </summary>
    internal static Expression<Func<T, PagedRow<TResult>>> BuildPaged<T, TResult>(
        this Expression<Func<T, TResult>> selector,
        IQueryable<T> countSource
    )
    {
        // Build an expression node that represents Queryable.Count<T>(countSource).
        // EF Core translates this into a scalar SQL sub-query: (SELECT COUNT(*) FROM ... WHERE ...).
        // countSource.Expression carries the full filtered IQueryable (filters applied, no Skip/Take),
        // so the COUNT covers all matching rows regardless of paging.
        var countCall = Expression.Call(
            typeof(Queryable), // static class containing the method
            nameof(Queryable.Count), // method name to call
            [typeof(T)], // generic type argument: Count<T>
            countSource.Expression // the IQueryable expression tree as the argument
        );

        // Get the PagedRow<TResult>(TResult item, int totalCount) constructor via reflection
        // so we can build a "new PagedRow<TResult>(...)" expression node.
        var ctor =
            typeof(PagedRow<TResult>).GetConstructor([typeof(TResult), typeof(int)])
            ?? throw new InvalidOperationException(
                $"No suitable constructor found for {typeof(PagedRow<TResult>)} with parameters (TResult, int)."
            );

        // Combine: new PagedRow<TResult>(selector.Body, countCall)
        //   - selector.Body is the original projection (e.g. new ProductResponse(product.Name, ...))
        //   - countCall is the scalar COUNT sub-query expression built above
        // EF Core sees this as a single SELECT with an inline sub-query for the count column.
        var newExpr = Expression.New(ctor, selector.Body, countCall);

        // Reuse the lambda parameter from the original selector (e.g. the "product" in product => new ProductResponse(...)).
        // This ensures the new combined expression operates on the same entity parameter that EF Core already understands.
        var entityParam = selector.Parameters[0];

        // Wrap everything into a lambda: entity => new PagedRow<TResult>(projection(entity), COUNT(*))
        // This is the final expression that replaces the original .Select() projection,
        // producing rows that carry both the projected DTO and the total count.
        return Expression.Lambda<Func<T, PagedRow<TResult>>>(newExpr, entityParam);
    }
}
