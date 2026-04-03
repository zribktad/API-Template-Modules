using Ardalis.Specification;
using ProductCatalog.Features.GetProducts;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Shared;

/// <summary>
/// Ardalis specification used for the price facet query; applies all filter criteria except the price range so that all price buckets remain visible regardless of the selected price filter.
/// </summary>
public sealed class ProductPriceFacetSpecification : Specification<ProductEntity>
{
    public ProductPriceFacetSpecification(ProductFilter filter)
    {
        Query.ApplyFilter(filter, new ProductFilterCriteriaOptions(IgnorePriceRange: true));

        Query.AsNoTracking();
    }
}
