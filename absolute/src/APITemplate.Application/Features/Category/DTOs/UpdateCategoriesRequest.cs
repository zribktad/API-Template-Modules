using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Category.DTOs;

/// <summary>
/// Carries a list of category items to be updated in a single batch operation; accepts between 1 and 100 items.
/// </summary>
public sealed record UpdateCategoriesRequest(
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "Maximum 100 items per batch.")]
        IReadOnlyList<UpdateCategoryItem> Items
);

/// <summary>
/// Represents a single category within a batch update request, including its ID and replacement data.
/// </summary>
public sealed record UpdateCategoryItem(
    [NotEmpty(ErrorMessage = "Category ID is required.")] Guid Id,
    [NotEmpty(ErrorMessage = "Category name is required.")]
    [MaxLength(200, ErrorMessage = "Category name must not exceed 200 characters.")]
        string Name,
    string? Description
) : IHasId;
