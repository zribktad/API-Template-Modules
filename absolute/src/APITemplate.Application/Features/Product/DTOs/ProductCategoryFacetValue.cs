namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Represents a single category bucket in the product search facets, containing the category identity and the number of matching products.
/// </summary>
public sealed record ProductCategoryFacetValue(Guid? CategoryId, string CategoryName, int Count);
