namespace SharedKernel.Domain.Entities.Contracts;

/// <summary>
/// Marks a domain entity as auditable, requiring it to expose an <see cref="AuditInfo"/> owned object
/// that records creation and last-modification metadata.
/// </summary>
public interface IAuditableEntity
{
    AuditInfo Audit { get; set; }
}
