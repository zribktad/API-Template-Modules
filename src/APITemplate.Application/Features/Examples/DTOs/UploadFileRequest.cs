namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Carries all data needed to store an uploaded file, including the raw stream, original file name, content type, size, and optional description.
/// </summary>
public sealed record UploadFileRequest(
    Stream FileStream,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Description
);
