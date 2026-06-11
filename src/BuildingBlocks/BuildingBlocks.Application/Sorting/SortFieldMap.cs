using System.Linq.Expressions;
using System.Reflection;
using Ardalis.Specification;

namespace BuildingBlocks.Application.Sorting;

/// <summary>
///     Fluent builder that maps named <see cref="SortField" /> values to strongly-typed key-selector expressions
///     and applies the resulting <c>OrderBy</c> / <c>OrderByDescending</c> clause to an Ardalis Specification query.
/// </summary>
public sealed class SortFieldMap<TEntity>
    where TEntity : class
{
    // Deterministic tiebreaker on the primary key so pages stay stable when the chosen sort key has
    // ties (e.g. batch-inserted rows sharing CreatedAtUtc, or non-unique Name/Price/Rating). Without
    // it PostgreSQL gives no stable order among ties and Skip/Take can duplicate or drop rows.
    private static readonly Expression<Func<TEntity, object?>>? IdTiebreaker = BuildIdTiebreaker();

    private readonly List<Entry> _entries = [];
    private Expression<Func<TEntity, object?>>? _default;

    /// <summary>Returns the collection of registered sort field names that callers are permitted to use.</summary>
    public IReadOnlyCollection<string> AllowedNames =>
        _entries.Select(e => e.Field.Value).ToArray();

    /// <summary>
    ///     Registers a named sort field paired with its key-selector expression and returns <see langword="this" /> for
    ///     chaining.
    /// </summary>
    public SortFieldMap<TEntity> Add(
        SortField field,
        Expression<Func<TEntity, object?>> keySelector
    )
    {
        _entries.Add(new Entry(field, keySelector));
        return this;
    }

    /// <summary>Sets the fallback key-selector applied when no recognised sort field is supplied by the caller.</summary>
    public SortFieldMap<TEntity> Default(Expression<Func<TEntity, object?>> keySelector)
    {
        _default = keySelector;
        return this;
    }

    /// <summary>
    ///     Resolves the appropriate key selector from <paramref name="sortBy" /> and appends an
    ///     <c>OrderBy</c> or <c>OrderByDescending</c> clause to <paramref name="query" />.
    ///     Defaults to descending order; uses the fallback key selector when <paramref name="sortBy" />
    ///     is unrecognised or <see langword="null" />.
    /// </summary>
    public void ApplySort(
        ISpecificationBuilder<TEntity> query,
        string? sortBy,
        string? sortDirection
    )
    {
        bool desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        Expression<Func<TEntity, object?>>? key =
            _entries.FirstOrDefault(e => e.Field.Matches(sortBy)).KeySelector ?? _default;

        if (key is null)
            return;

        IOrderedSpecificationBuilder<TEntity> ordered = desc
            ? query.OrderByDescending(key)
            : query.OrderBy(key);

        // Append the primary-key tiebreaker unless the primary sort already is the key.
        if (IdTiebreaker is not null && !ReferenceEquals(key, IdTiebreaker))
            ordered.ThenBy(IdTiebreaker);
    }

    private static Expression<Func<TEntity, object?>>? BuildIdTiebreaker()
    {
        PropertyInfo? idProperty = typeof(TEntity).GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance
        );
        if (idProperty is null)
            return null;

        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression body = Expression.Convert(
            Expression.Property(parameter, idProperty),
            typeof(object)
        );
        return Expression.Lambda<Func<TEntity, object?>>(body, parameter);
    }

    private readonly record struct Entry(
        SortField Field,
        Expression<Func<TEntity, object?>> KeySelector
    );
}
