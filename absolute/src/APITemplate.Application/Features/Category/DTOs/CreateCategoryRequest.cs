using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Category.DTOs;

/// <summary>
/// Payload for creating a new category, carrying the name and optional description.
/// </summary>
public sealed record CreateCategoryRequest(
    [NotEmpty(ErrorMessage = "Category name is required.")]
    [MaxLength(200, ErrorMessage = "Category name must not exceed 200 characters.")]
        string Name,
    string? Description
);
