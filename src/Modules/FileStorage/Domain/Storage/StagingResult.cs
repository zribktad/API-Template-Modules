namespace FileStorage.Domain.Storage;

/// <summary>
///     Outcome of <see cref="IBlobStore.WriteStagingAsync" />: the path under which the payload is temporarily
///     held, the streaming SHA-256 digest, and the byte count observed while the stream was drained.
/// </summary>
public sealed record StagingResult(string StagingPath, string Sha256, long SizeBytes);
