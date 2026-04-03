namespace SharedKernel.Contracts.Queries.ProductCatalog;

/// <summary>
/// Cross-module query that validates whether a product with the given identifier exists.
/// Handled by the ProductCatalog module.
/// </summary>
public sealed record ValidateProductExistsQuery(Guid ProductId);
