using Ardalis.Specification;
using ProductCatalog.Entities;
using ProductCatalog.Features.Category.Shared;

namespace ProductCatalog.Features.Category.GetCategoryById;

/// <summary>
///     Specification for retrieving multiple categories by their IDs.
/// </summary>
public sealed class CategoriesByIdsSpecification
    : Specification<Entities.Category, CategoryResponse>
{
    public CategoriesByIdsSpecification(IEnumerable<Guid> ids)
    {
        Query
            .Where(c => ids.Contains(c.Id))
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Description, c.Audit.CreatedAtUtc));
    }
}
