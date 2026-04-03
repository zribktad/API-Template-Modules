using Ardalis.Specification;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Product.GetProductById;

/// <summary>
/// Ardalis specification that fetches a single product by its ID and projects it directly to a <see cref="ProductResponse"/> DTO.
/// </summary>
public sealed class ProductByIdSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductByIdSpecification(Guid id)
    {
        Query.Where(product => product.Id == id).Select(ProductMappings.Projection);
    }
}
