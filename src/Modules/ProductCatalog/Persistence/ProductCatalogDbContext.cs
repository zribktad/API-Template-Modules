using Microsoft.EntityFrameworkCore;
using ProductCatalog.Persistence.Interceptors;
using SharedKernel.Infrastructure.Auditing;

namespace ProductCatalog.Persistence;

public sealed class ProductCatalogDbContext : ModuleDbContext
{
    private readonly IActorProvider _actor;
    private readonly TimeProvider _time;

    public ProductCatalogDbContext(
        DbContextOptions<ProductCatalogDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager)
    {
        _actor = actorProvider;
        _time = timeProvider;
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductDataLink> ProductDataLinks => Set<ProductDataLink>();
    public DbSet<ProductCategoryStats> ProductCategoryStats => Set<ProductCategoryStats>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new ProductLinkSoftDeleteCascadeInterceptor(_actor, _time));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductCatalogDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
