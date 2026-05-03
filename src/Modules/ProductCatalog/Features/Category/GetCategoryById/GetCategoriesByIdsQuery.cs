using ErrorOr;
using ProductCatalog.Features.Category.Shared;

namespace ProductCatalog.Features.Category.GetCategoryById;

/// <summary>
///     Query to retrieve multiple categories by their IDs.
/// </summary>
public sealed record GetCategoriesByIdsQuery(IReadOnlyList<Guid> Ids);

/// <summary>
///     Handles <see cref="GetCategoriesByIdsQuery" />.
/// </summary>
public sealed class GetCategoriesByIdsQueryHandler
{
    public static async Task<ErrorOr<IReadOnlyDictionary<Guid, CategoryResponse>>> HandleAsync(
        GetCategoriesByIdsQuery request,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        if (request.Ids.Count == 0)
        {
            return (ErrorOr<IReadOnlyDictionary<Guid, CategoryResponse>>)
                new Dictionary<Guid, CategoryResponse>();
        }

        List<CategoryResponse> categories = await repository.ListAsync(
            new CategoriesByIdsSpecification(request.Ids),
            ct
        );

        return (ErrorOr<IReadOnlyDictionary<Guid, CategoryResponse>>)
            categories.ToDictionary(c => c.Id);
    }
}
