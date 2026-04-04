using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;

namespace ProductCatalog.Persistence;

public sealed class ProductCatalogDbContext : ModuleDbContext
{
    public ProductCatalogDbContext(
        DbContextOptions<ProductCatalogDbContext> options,
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
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductDataLink> ProductDataLinks => Set<ProductDataLink>();
    public DbSet<ProductCategoryStats> ProductCategoryStats => Set<ProductCategoryStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductCatalogDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
