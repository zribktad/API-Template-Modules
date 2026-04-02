namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Carries the unique identifier of the stored file to be downloaded.
/// </summary>
public sealed record DownloadFileRequest(Guid Id) : IHasId;
