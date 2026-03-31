using SharedKernel.Application.Context;
using APITemplate.Domain.Entities;
using SharedKernel.Infrastructure.Persistence;
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
public sealed class AppDbContext : ModuleDbContext
{
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
        : base(
            options,
            tenantProvider,
            actorProvider,
            timeProvider,
            softDeleteCascadeRules,
            entityNormalizationService,
            entityStateManager,
            softDeleteProcessor
        ) { }

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
}
