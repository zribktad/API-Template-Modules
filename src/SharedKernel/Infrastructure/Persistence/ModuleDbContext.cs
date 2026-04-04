using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel.Application.Context;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.SoftDelete;

namespace SharedKernel.Infrastructure.Persistence;

/// <summary>
///     Base EF Core context for modules that need multi-tenancy, audit stamping, soft delete, and optimistic concurrency.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly IActorProvider _actorProvider;
    private readonly IEntityNormalizationService _entityNormalizationService;
    private readonly IAuditableEntityStateManager _entityStateManager;
    private readonly IReadOnlyCollection<ISoftDeleteCascadeRule> _softDeleteCascadeRules;
    private readonly ISoftDeleteProcessor _softDeleteProcessor;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeProvider _timeProvider;

    protected ModuleDbContext(
        DbContextOptions options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IEnumerable<ISoftDeleteCascadeRule> softDeleteCascadeRules,
        IEntityNormalizationService entityNormalizationService,
        IAuditableEntityStateManager entityStateManager,
        ISoftDeleteProcessor softDeleteProcessor
    )
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _actorProvider = actorProvider;
        _timeProvider = timeProvider;
        _softDeleteCascadeRules = softDeleteCascadeRules.ToList();
        _entityNormalizationService = entityNormalizationService;
        _entityStateManager = entityStateManager;
        _softDeleteProcessor = softDeleteProcessor;
    }

    protected Guid CurrentTenantId => _tenantProvider.TenantId;
    protected bool HasTenant => _tenantProvider.HasTenant;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException(
            "Use SaveChangesAsync to avoid deadlocks from async soft-delete cascade rules. "
                + "All application paths should go through IUnitOfWork.CommitAsync()."
        );
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        await ApplyEntityAuditingAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (
                !typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)
                || !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
            )
                continue;

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

    private async Task ApplyEntityAuditingAsync(CancellationToken cancellationToken)
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
                    _entityNormalizationService.Normalize(entity);
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
                    _entityNormalizationService.Normalize(entity);
                    _entityStateManager.StampModified(entity, now, actor);
                    break;
                case EntityState.Deleted:
                    await _softDeleteProcessor.ProcessAsync(
                        this,
                        entry,
                        entity,
                        now,
                        actor,
                        _softDeleteCascadeRules,
                        cancellationToken
                    );
                    break;
            }
        }
    }
}
