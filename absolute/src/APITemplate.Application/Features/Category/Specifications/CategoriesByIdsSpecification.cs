using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

/// <summary>
/// Ardalis specification that loads multiple categories by their IDs, used for batch update and delete operations.
/// </summary>
public sealed class CategoriesByIdsSpecification : Specification<CategoryEntity>
{
    public CategoriesByIdsSpecification(IReadOnlyCollection<Guid> ids)
    {
        Query.Where(category => ids.Contains(category.Id));
    }
}
