using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Features.CreateCategories;

/// <summary>
/// Carries a list of category items to be created in a single batch operation; accepts between 1 and 100 items.
/// </summary>
public sealed record CreateCategoriesRequest(
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "Maximum 100 items per batch.")]
        IReadOnlyList<CreateCategoryRequest> Items
);
