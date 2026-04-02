namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Represents the metadata of a successfully uploaded file as returned to the API consumer.
/// </summary>
public sealed record FileUploadResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAtUtc
) : IHasId;
