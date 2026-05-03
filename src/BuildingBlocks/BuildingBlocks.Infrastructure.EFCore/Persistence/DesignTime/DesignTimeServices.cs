using Microsoft.EntityFrameworkCore.ChangeTracking;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Domain.Entities.Contracts;
using BuildingBlocks.Infrastructure.EFCore.Auditing;

namespace BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;

/// <summary>
///     No-op implementations of <see cref="ModuleDbContext" /> dependencies for EF Core design-time factories.
/// </summary>
public static class DesignTimeServices
{
    public static ITenantProvider TenantProvider { get; } = new NoOpTenantProvider();
    public static IActorProvider ActorProvider { get; } = new NoOpActorProvider();
    public static IAuditableEntityStateManager AuditableEntityStateManager { get; } =
        new NoOpAuditableEntityStateManager();

    private sealed class NoOpTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class NoOpActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }

    private sealed class NoOpAuditableEntityStateManager : IAuditableEntityStateManager
    {
        public void StampAdded(
            EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor,
            bool hasTenant,
            Guid currentTenantId
        ) { }

        public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor) { }

        public void MarkSoftDeleted(
            EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor
        ) { }
    }
}

