using Ardalis.Specification;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Product.Shared;

/// <summary>
///     Ardalis specification that loads multiple products by their IDs without eager-loading
///     navigation properties, used for validation-only batch operations.
/// </summary>
public sealed class ProductsByIdsSpecification : Specification<ProductEntity>
{
    public ProductsByIdsSpecification(IReadOnlyCollection<Guid> ids)
    {
        Query.Where(product => ids.Contains(product.Id));
    }
}
