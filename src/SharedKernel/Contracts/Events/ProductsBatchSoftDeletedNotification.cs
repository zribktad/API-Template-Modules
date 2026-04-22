namespace SharedKernel.Contracts.Events;

/// <summary>
///     Published after one or more products are soft-deleted, allowing downstream handlers
///     to trigger cascading cleanup across modules (e.g. Reviews) in a single batch operation.
///     <c>TenantId</c> is required because Wolverine durable-local-queue dispatch runs the
///     handler outside the HTTP scope, so <c>ITenantProvider.HasTenant</c> is <c>false</c> and
///     global tenant filters cannot resolve the tenant — downstream handlers must scope writes
///     explicitly via this field.
/// </summary>
public sealed record ProductsBatchSoftDeletedNotification(
    IReadOnlyList<Guid> ProductIds,
    Guid TenantId,
    Guid ActorId,
    DateTime DeletedAtUtc,
    Guid CorrelationId
);
