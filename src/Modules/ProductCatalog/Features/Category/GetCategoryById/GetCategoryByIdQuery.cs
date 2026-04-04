using ErrorOr;

namespace ProductCatalog.Features.Category.GetCategoryById;

/// <summary>Returns a single category by its unique identifier, or <see langword="null" /> if not found.</summary>
public sealed record GetCategoryByIdQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetCategoryByIdQuery" />.</summary>
public sealed class GetCategoryByIdQueryHandler
{
    public static async Task<ErrorOr<CategoryResponse>> HandleAsync(
        GetCategoryByIdQuery request,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        CategoryResponse? result = await repository.FirstOrDefaultAsync(
            new CategoryByIdSpecification(request.Id),
            ct
        );

        if (result is null)
            return DomainErrors.Categories.NotFound(request.Id);

        return result;
    }
}
