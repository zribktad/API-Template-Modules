namespace SharedKernel.Contracts.Queries.Reviews;

/// <summary>
///     Cross-module query that returns reviews grouped by product ID for a batch of product
///     identifiers. Used by the ProductCatalog GraphQL DataLoader to batch-load reviews.
///     Handled by the Reviews module.
/// </summary>
public sealed record GetProductReviewsByProductIdsQuery(IReadOnlyCollection<Guid> ProductIds);
