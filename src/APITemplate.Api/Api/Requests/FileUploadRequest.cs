using System.ComponentModel.DataAnnotations;

namespace APITemplate.Api.Requests;

/// <summary>
/// Represents the multipart form-data payload for a file upload endpoint,
/// carrying the required file stream and an optional free-text description.
/// </summary>
public sealed class FileUploadRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;

    public string? Description { get; init; }
}
