using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace APITemplate.Api.Features.Examples.DTOs;

/// <summary>
/// Mutable DTO used as the patch target for JSON Patch operations on a product; declared as a class
/// rather than a record because JSON Patch mutates the object in-place.
/// </summary>
public sealed class PatchableProductDto
{
    [NotEmpty(ErrorMessage = "Name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be positive.")]
    public decimal Price { get; set; }

    public Guid? CategoryId { get; set; }
}
