using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Product.Shared;

/// <summary>
///     Defines the allowed sort fields for product queries and provides the <see cref="SortFieldMap{T}" /> used by
///     specifications to apply ordering.
/// </summary>
public static class ProductSortFields
{
    public const string NameToken = nameof(Name);
    public const string PriceToken = nameof(Price);
    public const string CreatedAtToken = nameof(CreatedAt);

    public static readonly SortField Name = new(NameToken);
    public static readonly SortField Price = new(PriceToken);
    public static readonly SortField CreatedAt = new(CreatedAtToken);

    public static readonly SortFieldMap<ProductEntity> Map = new SortFieldMap<ProductEntity>()
        .Add(Name, p => p.Name)
        .Add(Price, p => p.Price)
        .Add(CreatedAt, p => p.Audit.CreatedAtUtc)
        .Default(p => p.Audit.CreatedAtUtc);
}
