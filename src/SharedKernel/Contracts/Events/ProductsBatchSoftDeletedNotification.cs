namespace SharedKernel.Contracts.Events;

/// <summary>
///     Published after one or more products are soft-deleted, allowing downstream handlers
///     to trigger cascading cleanup across modules (e.g. Reviews) in a single batch operation.
/// </summary>
public sealed record ProductsBatchSoftDeletedNotification(
    IReadOnlyList<Guid> ProductIds,
    Guid ActorId,
    DateTime DeletedAtUtc,
    Guid CorrelationId
);
