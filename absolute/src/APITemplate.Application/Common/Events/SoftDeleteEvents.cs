namespace APITemplate.Application.Common.Events;

/// <summary>
/// Published after a tenant is soft-deleted, allowing downstream handlers to trigger
/// cascading cleanup or audit logging without coupling the delete command to those concerns.
/// </summary>
public sealed record TenantSoftDeletedNotification(
    Guid TenantId,
    Guid ActorId,
    DateTime DeletedAtUtc
);
