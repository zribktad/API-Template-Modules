using CategoryEntity = ProductCatalog.Entities.Category;

namespace ProductCatalog.Features.Category.Shared;

/// <summary>
///     Defines the allowed sort fields for category queries and maps them to entity expressions.
/// </summary>
public static class CategorySortFields
{
    public const string NameToken = nameof(Name);
    public const string CreatedAtToken = nameof(CreatedAt);

    /// <summary>Sort by category name.</summary>
    public static readonly SortField Name = new(NameToken);

    /// <summary>Sort by creation timestamp.</summary>
    public static readonly SortField CreatedAt = new(CreatedAtToken);

    /// <summary>
    ///     The sort field map used to resolve and apply sorting to category specifications.
    ///     Defaults to sorting by <see cref="CreatedAt" /> when no sort field is specified.
    /// </summary>
    public static readonly SortFieldMap<CategoryEntity> Map = new SortFieldMap<CategoryEntity>()
        .Add(Name, c => c.Name)
        .Add(CreatedAt, c => c.Audit.CreatedAtUtc)
        .Default(c => c.Audit.CreatedAtUtc);
}
