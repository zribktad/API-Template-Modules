using ErrorOr;
using Npgsql;
using ProductCategoryStatsEntity = ProductCatalog.Entities.ProductCategoryStats;

namespace ProductCatalog.Features.Category.GetCategoryStats;

/// <summary>Returns aggregated statistics for a category by its identifier, or <see langword="null" /> if not found.</summary>
public sealed record GetCategoryStatsQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetCategoryStatsQuery" />.</summary>
public sealed class GetCategoryStatsQueryHandler
{
    public static async Task<ErrorOr<ProductCategoryStatsResponse>> HandleAsync(
        GetCategoryStatsQuery request,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        ProductCategoryStatsEntity? stats;
        try
        {
            stats = await repository.GetStatsByIdAsync(request.Id, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedFunction)
        {
            ProductCatalog.Entities.Category? category = await repository.GetByIdAsync(
                request.Id,
                ct
            );
            if (category is null)
                return DomainErrors.Categories.NotFound(request.Id);

            return new ProductCategoryStatsResponse(category.Id, category.Name, 0, 0m, 0);
        }

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
