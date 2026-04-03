using ErrorOr;
using ProductCatalog.Interfaces;
using ProductCategoryStatsEntity = ProductCatalog.Entities.ProductCategoryStats;

namespace ProductCatalog.Features.GetCategoryStats;

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
        ProductCategoryStatsEntity? stats = await repository.GetStatsByIdAsync(request.Id, ct);

        if (stats is null)
            return DomainErrors.Categories.NotFound(request.Id);

        return new ProductCategoryStatsResponse(
            stats.CategoryId,
            stats.CategoryName,
            stats.ProductCount,
            stats.AveragePrice,
            stats.TotalReviews
        );
    }
}
