using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Main EF Core context for relational storage.
/// Enforces multi-tenancy, audit stamping, soft delete, and optimistic concurrency
/// for all entities based on <see cref="IAuditableTenantEntity"/>.
/// </summary>
/// <remarks>
/// Key behavior:
/// <list type="bullet">
/// <item>
/// <description>
/// Global query filters automatically limit reads to the current tenant and exclude soft-deleted rows.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="SaveChanges(bool)"/> and <see cref="SaveChangesAsync(bool, CancellationToken)"/> centralize
/// audit field updates (<c>Created*</c>/<c>Updated*</c>/<c>Deleted*</c>).
/// </description>
/// </item>
/// <item>
/// <description>
/// Delete operations are converted to soft delete updates, including soft-cascade from Product to ProductReviews.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class AppDbContext : DbContext
{
    // Tenant provider drives read isolation (global filters) and default tenant assignment on inserts.
    private readonly ITenantProvider _tenantProvider;
    private readonly IActorProvider _actorProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyCollection<ISoftDeleteCascadeRule> _softDeleteCascadeRules;
    private readonly IEntityNormalizationService _entityNormalizationService;
    private readonly IAuditableEntityStateManager _entityStateManager;
    private readonly ISoftDeleteProcessor _softDeleteProcessor;

    private Guid CurrentTenantId => _tenantProvider.TenantId;
    private bool HasTenant => _tenantProvider.HasTenant;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
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

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductDataLink> ProductDataLinks => Set<ProductDataLink>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();
    public DbSet<ProductCategoryStats> ProductCategoryStats => Set<ProductCategoryStats>();
    public DbSet<FailedEmail> FailedEmails => Set<FailedEmail>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    /// <summary>
    /// Applies entity configurations and auto-registers global tenant/soft-delete query filters.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }

    /// <summary>
    /// Not supported — use <see cref="SaveChangesAsync(bool, CancellationToken)"/> instead.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown to prevent sync-over-async deadlocks
    /// caused by the async soft-delete cascade rules.</exception>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException(
            "Use SaveChangesAsync to avoid deadlocks from async soft-delete cascade rules. "
                + "All application paths should go through IUnitOfWork.CommitAsync()."
        );
    }

    /// <summary>
    /// Applies audit/soft-delete rules before committing changes asynchronously.
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        await ApplyEntityAuditingAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Discovers all model types implementing tenant + soft-delete contracts
    /// and wires a generic global filter for each of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core does not have a built-in way to register a generic query filter for "all
    /// types that implement these interfaces". This method finds those types at model
    /// build time and applies the filter using reflection.
    /// </para>
    /// <para>
    /// The applied filters ensure:
    ///  - soft-deleted rows (IsDeleted == true) are excluded implicitly,
    ///  - and multi-tenant reads are scoped to the current tenant.
    /// </para>
    /// </remarks>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        // Iterate over every entity type that EF Core knows about.
        // This includes all DbSet<> entities and entities registered via configurations.

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Only apply the filter to entities that support both multi-tenancy and soft delete.
            if (
                !typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)
                || !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
            )
            {
                continue;
            }

            // The SetGlobalFilter method is generic (<TEntity>). We only know the concrete type
            // at runtime, so we construct a closed generic method using reflection.
            var method = typeof(AppDbContext)
                .GetMethod(
                    nameof(SetGlobalFilter),
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!
                .MakeGenericMethod(entityType.ClrType);

            // Invoke the configured generic helper, applying the query filters to the model.
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetGlobalFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity, ISoftDeletable
    {
        modelBuilder
            .Entity<TEntity>()
            .HasQueryFilter("SoftDelete", entity => !entity.IsDeleted)
            .HasQueryFilter("Tenant", entity => HasTenant && entity.TenantId == CurrentTenantId);
    }

    /// <summary>
    /// Processes tracked entities and stamps audit fields according to current state.
    /// </summary>
    private async Task ApplyEntityAuditingAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var actor = _actorProvider.ActorId;

        foreach (
            var entry in ChangeTracker
                .Entries()
                .Where(e => e.Entity is IAuditableTenantEntity)
                .ToList()
        )
        {
            var entity = (IAuditableTenantEntity)entry.Entity;
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
