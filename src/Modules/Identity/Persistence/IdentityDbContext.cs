using Microsoft.EntityFrameworkCore;

namespace Identity.Persistence;

public sealed class IdentityDbContext : ModuleDbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IEntityNormalizationService entityNormalizationService,
        IAuditableEntityStateManager entityStateManager
    )
        : base(
            options,
            tenantProvider,
            actorProvider,
            timeProvider,
            entityNormalizationService,
            entityStateManager
        ) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
