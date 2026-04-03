using SharedKernel.Domain.Entities.Contracts;

namespace FileStorage.Shared;

/// <summary>
/// Carries the unique identifier of the stored file to be downloaded.
/// </summary>
public sealed record DownloadFileRequest(Guid Id) : IHasId;


