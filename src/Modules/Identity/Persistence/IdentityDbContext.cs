using Microsoft.EntityFrameworkCore;

namespace Identity.Persistence;

public sealed class IdentityDbContext : ModuleDbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();
    public DbSet<BffPersistedSession> BffSessions => Set<BffPersistedSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
