using BuildingBlocks.Application.Context;
using BuildingBlocks.Infrastructure.EFCore.Auditing;
using BuildingBlocks.Infrastructure.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain;

namespace Notifications.Persistence;

public sealed class NotificationsDbContext : ModuleDbContext
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

    public DbSet<FailedEmail> FailedEmails => Set<FailedEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
