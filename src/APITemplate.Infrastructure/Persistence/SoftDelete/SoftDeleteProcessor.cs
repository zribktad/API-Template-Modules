namespace APITemplate.Infrastructure.Persistence.SoftDelete;

/// <summary>
/// Legacy namespace wrapper for the SharedKernel soft-delete processor implementation.
/// </summary>
public sealed class SoftDeleteProcessor
    : SharedKernel.Infrastructure.SoftDelete.SoftDeleteProcessor,
        ISoftDeleteProcessor
{
    public SoftDeleteProcessor(Auditing.IAuditableEntityStateManager stateManager)
        : base(stateManager) { }
}
