using BuildingBlocks.Domain.Entities.Contracts;

namespace SharedKernel.Contracts.Queries.Reviews;

/// <summary>
///     Cross-module read model for a product review, used by the ProductCatalog DataLoader
///     to resolve the <c>reviews</c> field on the Product GraphQL type without a direct
///     assembly reference to the Reviews module.
/// </summary>
public sealed record ProductReviewResponse(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    string? Comment,
    int Rating,
    DateTime CreatedAtUtc
) : IHasId;
