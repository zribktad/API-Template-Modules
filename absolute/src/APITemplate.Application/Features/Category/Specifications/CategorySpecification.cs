using APITemplate.Application.Features.Category.Mappings;
using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

/// <summary>
/// Ardalis specification for querying a filtered and sorted list of categories projected to <see cref="CategoryResponse"/>.
/// </summary>
public sealed class CategorySpecification : Specification<CategoryEntity, CategoryResponse>
{
    /// <summary>Initialises the specification by applying filter, sort, and projection from <paramref name="filter"/>.</summary>
    public CategorySpecification(CategoryFilter filter)
    {
        Query.ApplyFilter(filter);
        Query.AsNoTracking();
        CategorySortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);
        Query.Select(CategoryMappings.Projection);
    }
}
