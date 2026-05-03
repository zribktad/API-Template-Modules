using BuildingBlocks.Domain.Entities.Contracts;

namespace FileStorage.Contracts;

/// <summary>
///     Carries the unique identifier of the stored file to be downloaded.
/// </summary>
public sealed record DownloadFileRequest(Guid Id) : IHasId;
