using APITemplate.Application.Features.Category.Mappings;
using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

/// <summary>
/// Ardalis specification that fetches a single category by its identifier, projected directly to <see cref="CategoryResponse"/>.
/// </summary>
public sealed class CategoryByIdSpecification : Specification<CategoryEntity, CategoryResponse>
{
    /// <summary>Initialises the specification for the given <paramref name="id"/>.</summary>
    public CategoryByIdSpecification(Guid id)
    {
        Query
            .Where(category => category.Id == id)
            .AsNoTracking()
            .Select(CategoryMappings.Projection);
    }
}
