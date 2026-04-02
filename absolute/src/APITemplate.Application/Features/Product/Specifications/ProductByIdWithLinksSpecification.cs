using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

/// <summary>
/// Ardalis specification that loads a product by ID and eagerly includes its <c>ProductDataLinks</c> collection, used when link synchronisation or deletion is required.
/// </summary>
public sealed class ProductByIdWithLinksSpecification : Specification<ProductEntity>
{
    public ProductByIdWithLinksSpecification(Guid id)
    {
        Query.Where(product => product.Id == id).Include(product => product.ProductDataLinks);
    }
}
