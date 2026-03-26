using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

/// <summary>
/// Ardalis specification used for the category facet query; applies all filter criteria except category-ID filtering so that counts reflect the full category distribution.
/// </summary>
public sealed class ProductCategoryFacetSpecification : Specification<ProductEntity>
{
    public ProductCategoryFacetSpecification(ProductFilter filter)
    {
        Query.ApplyFilter(filter, new ProductFilterCriteriaOptions(IgnoreCategoryIds: true));

        Query.AsNoTracking();
    }
}
