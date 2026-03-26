namespace APITemplate.Application.Features.Category.DTOs;

/// <summary>
/// Aggregated statistics for a single category, including product count, average price, and total review count.
/// </summary>
public sealed record ProductCategoryStatsResponse(
    Guid CategoryId,
    string CategoryName,
    long ProductCount,
    decimal AveragePrice,
    long TotalReviews
);
