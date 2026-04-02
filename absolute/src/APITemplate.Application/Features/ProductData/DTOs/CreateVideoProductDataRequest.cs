using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductData.DTOs;

/// <summary>
/// Payload for uploading video product data, including duration, resolution, format, and file size.
/// Validation constraints are expressed via data annotations and enforced by <c>CreateVideoProductDataRequestValidator</c>.
/// </summary>
public sealed record CreateVideoProductDataRequest(
    [NotEmpty(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
        string Title,
    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters.")]
        string? Description,
    [Range(1, int.MaxValue, ErrorMessage = "DurationSeconds must be greater than zero.")]
        int DurationSeconds,
    [NotEmpty(ErrorMessage = "Resolution is required.")]
    [AllowedValues(
        "720p",
        "1080p",
        "4K",
        ErrorMessage = "Resolution must be one of: 720p, 1080p, 4K."
    )]
        string Resolution,
    [NotEmpty(ErrorMessage = "Format is required.")]
    [AllowedValues("mp4", "avi", "mkv", ErrorMessage = "Format must be one of: mp4, avi, mkv.")]
        string Format,
    [Range(1, long.MaxValue, ErrorMessage = "FileSizeBytes must be greater than zero.")]
        long FileSizeBytes
);
