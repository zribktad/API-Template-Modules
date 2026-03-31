using System.Linq.Expressions;
using Ardalis.Specification;

namespace APITemplate.Application.Common.Sorting;

/// <summary>
/// Fluent builder that maps named <see cref="SortField"/> values to strongly-typed key-selector expressions
/// and applies the resulting <c>OrderBy</c> / <c>OrderByDescending</c> clause to an Ardalis Specification query.
/// </summary>
public sealed class SortFieldMap<TEntity>
    where TEntity : class
{
    private readonly record struct Entry(
        SortField Field,
        Expression<Func<TEntity, object?>> KeySelector
    );

    private readonly List<Entry> _entries = [];
    private Expression<Func<TEntity, object?>>? _default;

    /// <summary>Returns the collection of registered sort field names that callers are permitted to use.</summary>
    public IReadOnlyCollection<string> AllowedNames =>
        _entries.Select(e => e.Field.Value).ToArray();

    /// <summary>Registers a named sort field paired with its key-selector expression and returns <see langword="this"/> for chaining.</summary>
    public SortFieldMap<TEntity> Add(
        SortField field,
        Expression<Func<TEntity, object?>> keySelector
    )
    {
        _entries.Add(new(field, keySelector));
        return this;
    }

    /// <summary>Sets the fallback key-selector applied when no recognised sort field is supplied by the caller.</summary>
    public SortFieldMap<TEntity> Default(Expression<Func<TEntity, object?>> keySelector)
    {
        _default = keySelector;
        return this;
    }

    /// <summary>
    /// Resolves the appropriate key selector from <paramref name="sortBy"/> and appends an
    /// <c>OrderBy</c> or <c>OrderByDescending</c> clause to <paramref name="query"/>.
    /// Defaults to descending order; uses the fallback key selector when <paramref name="sortBy"/>
    /// is unrecognised or <see langword="null"/>.
    /// </summary>
    public void ApplySort(
        ISpecificationBuilder<TEntity> query,
        string? sortBy,
        string? sortDirection
    )
    {
        var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var key = _entries.FirstOrDefault(e => e.Field.Matches(sortBy)).KeySelector ?? _default;

        if (key is null)
            return;

        if (desc)
            query.OrderByDescending(key);
        else
            query.OrderBy(key);
    }
}
