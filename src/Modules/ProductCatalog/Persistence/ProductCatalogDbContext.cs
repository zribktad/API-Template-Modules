using BuildingBlocks.Infrastructure.EFCore.Auditing;
using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Persistence;

public sealed class ProductCatalogDbContext : ModuleDbContext
{
    public ProductCatalogDbContext(
        DbContextOptions<ProductCatalogDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

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
