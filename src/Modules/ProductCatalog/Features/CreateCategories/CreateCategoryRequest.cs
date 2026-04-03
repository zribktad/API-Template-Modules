using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.CreateCategories;

/// <summary>
/// Payload for creating a new category, carrying the name and optional description.
/// </summary>
public sealed record CreateCategoryRequest(
    [NotEmpty(ErrorMessage = "Category name is required.")]
    [MaxLength(200, ErrorMessage = "Category name must not exceed 200 characters.")]
        string Name,
    string? Description
);
