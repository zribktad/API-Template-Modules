namespace SharedKernel.Contracts.Commands.FileStorage;

/// <summary>
///     Cross-module command instructing the FileStorage module to sweep orphan staging payloads
///     (older than staging TTL) and zero-refcount blobs (older than the configured retention window).
///     Dispatched by the BackgroundJobs orphan-blob recurring job via the message bus.
/// </summary>
public sealed record SweepOrphanBlobsCommand();
