namespace APITemplate.Application.Features.ProductReview.DTOs;

/// <summary>
/// Read model returned by product review queries, representing a single review submitted by a user for a product.
/// </summary>
public sealed record ProductReviewResponse(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    string? Comment,
    int Rating,
    DateTime CreatedAtUtc
) : IHasId;
