using System.Reflection;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Domain.Entities.Contracts;
using BuildingBlocks.Infrastructure.EFCore.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildingBlocks.Infrastructure.EFCore.Persistence;

/// <summary>
///     Base EF Core context for modules that need multi-tenancy, audit stamping, soft delete, and optimistic concurrency.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly IActorProvider _actorProvider;
    private readonly IAuditableEntityStateManager _entityStateManager;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeProvider _timeProvider;

    protected ModuleDbContext(
        DbContextOptions options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _actorProvider = actorProvider;
        _timeProvider = timeProvider;
        _entityStateManager = entityStateManager;
    }

    protected Guid CurrentTenantId => _tenantProvider.TenantId;
    protected bool HasTenant => _tenantProvider.HasTenant;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException(
            "All application paths should go through IUnitOfWork.CommitAsync()."
        );
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        ApplyEntityAuditing();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (
                !typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)
                || !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
            )
            {
                continue;
            }

            MethodInfo method = typeof(ModuleDbContext)
                .GetMethod(nameof(SetGlobalFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetGlobalFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity, ISoftDeletable
    {
        modelBuilder
            .Entity<TEntity>()
            .HasQueryFilter(GlobalQueryFilterNames.SoftDelete, entity => !entity.IsDeleted)
            .HasQueryFilter(
                GlobalQueryFilterNames.Tenant,
                entity => HasTenant && entity.TenantId == CurrentTenantId
            );
    }

    private void ApplyEntityAuditing()
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Guid actor = _actorProvider.ActorId;

        foreach (
            EntityEntry? entry in ChangeTracker
                .Entries()
                .Where(e => e.Entity is IAuditableTenantEntity)
                .ToList()
        )
        {
            IAuditableTenantEntity entity = (IAuditableTenantEntity)entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    _entityStateManager.StampAdded(
                        entry,
                        entity,
                        now,
                        actor,
                        HasTenant,
                        CurrentTenantId
                    );
                    break;
                case EntityState.Modified:
                    _entityStateManager.StampModified(entity, now, actor);
                    break;
                case EntityState.Deleted:
                    _entityStateManager.MarkSoftDeleted(entry, entity, now, actor);
                    break;
            }
        }
    }
}
