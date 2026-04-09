using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;

namespace Notifications.Persistence;

public sealed class NotificationsDbContext : ModuleDbContext
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
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

    public DbSet<FailedEmail> FailedEmails => Set<FailedEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
