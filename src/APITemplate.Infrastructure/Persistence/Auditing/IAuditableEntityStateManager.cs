namespace APITemplate.Infrastructure.Persistence.Auditing;

/// <summary>
/// Legacy namespace shim for the SharedKernel audit state manager contract.
/// </summary>
public interface IAuditableEntityStateManager
    : SharedKernel.Infrastructure.Auditing.IAuditableEntityStateManager;
