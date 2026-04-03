using ErrorOr;
using ProductCatalog.Application.Features.Category.Mappings;
using ProductCatalog.Domain.Interfaces;

namespace ProductCatalog.Application.Features.Category;

/// <summary>Returns aggregated statistics for a category by its identifier, or <see langword="null"/> if not found.</summary>
public sealed record GetCategoryStatsQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetCategoryStatsQuery"/>.</summary>
public sealed class GetCategoryStatsQueryHandler
{
    public static async Task<ErrorOr<ProductCategoryStatsResponse>> HandleAsync(
        GetCategoryStatsQuery request,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        ProductCategoryStats? stats = await repository.GetStatsByIdAsync(request.Id, ct);

        if (stats is null)
            return DomainErrors.Categories.NotFound(request.Id);

        return stats.ToResponse();
    }
}
