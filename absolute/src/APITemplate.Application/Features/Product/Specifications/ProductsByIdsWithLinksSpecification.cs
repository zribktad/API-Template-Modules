using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

/// <summary>
/// Ardalis specification that loads multiple products by their IDs and eagerly includes
/// their <c>ProductDataLinks</c> collections, used for batch update and delete operations.
/// </summary>
public sealed class ProductsByIdsWithLinksSpecification : Specification<ProductEntity>
{
    public ProductsByIdsWithLinksSpecification(IReadOnlyCollection<Guid> ids)
    {
        Query
            .Where(product => ids.Contains(product.Id))
            .Include(product => product.ProductDataLinks);
    }
}
