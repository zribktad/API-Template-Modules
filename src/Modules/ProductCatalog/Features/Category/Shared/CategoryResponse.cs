namespace ProductCatalog.Features.Category.Shared;

/// <summary>
///     Read model returned by category queries, containing the public-facing representation of a category.
/// </summary>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc
) : IHasId;
