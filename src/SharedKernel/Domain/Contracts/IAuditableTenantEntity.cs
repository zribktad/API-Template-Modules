namespace SharedKernel.Domain.Entities.Contracts;

/// <summary>
/// Composite entity contract that combines tenant isolation, audit tracking, and soft-delete capability.
/// All first-class tenant-scoped domain entities implement this interface.
/// </summary>
public interface IAuditableTenantEntity : ITenantEntity, IAuditableEntity, ISoftDeletable { }
