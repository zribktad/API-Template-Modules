using Ardalis.Specification;
using ProductCatalog.Features.GetProducts;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Shared;

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
