namespace ProductCatalog.Features.Category.UpdateCategories;

/// <summary>
///     Payload for updating an existing category's name and optional description.
/// </summary>
public sealed record UpdateCategoryRequest(string Name, string? Description);
