namespace SharedKernel.Contracts.Events;

/// <summary>
///     Published after a product is soft-deleted, allowing downstream handlers
///     to trigger cascading cleanup or audit logging across modules (like Reviews or ProductData).
/// </summary>
public sealed record ProductSoftDeletedNotification(
    Guid ProductId,
    Guid ActorId,
    DateTime DeletedAtUtc
);
