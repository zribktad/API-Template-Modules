using APITemplate.Application.Common.Sorting;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

/// <summary>
/// Defines the allowed sort fields for product queries and provides the <see cref="SortFieldMap{T}"/> used by specifications to apply ordering.
/// </summary>
public static class ProductSortFields
{
    public static readonly SortField Name = new("name");
    public static readonly SortField Price = new("price");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<ProductEntity> Map = new SortFieldMap<ProductEntity>()
        .Add(Name, p => p.Name)
        .Add(Price, p => (object)p.Price)
        .Add(CreatedAt, p => p.Audit.CreatedAtUtc)
        .Default(p => p.Audit.CreatedAtUtc);
}
