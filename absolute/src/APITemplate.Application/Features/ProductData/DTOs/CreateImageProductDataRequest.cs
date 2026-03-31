using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductData.DTOs;

/// <summary>
/// Payload for uploading image product data, including dimensions, format, and file size.
/// Validation constraints are expressed via data annotations and enforced by <c>CreateImageProductDataRequestValidator</c>.
/// </summary>
public sealed record CreateImageProductDataRequest(
    [NotEmpty(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
        string Title,
    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters.")]
        string? Description,
    [Range(1, int.MaxValue, ErrorMessage = "Width must be greater than zero.")] int Width,
    [Range(1, int.MaxValue, ErrorMessage = "Height must be greater than zero.")] int Height,
    [NotEmpty(ErrorMessage = "Format is required.")]
    [AllowedValues(
        "jpg",
        "png",
        "gif",
        "webp",
        ErrorMessage = "Format must be one of: jpg, png, gif, webp."
    )]
        string Format,
    [Range(1, long.MaxValue, ErrorMessage = "FileSizeBytes must be greater than zero.")]
        long FileSizeBytes
);
