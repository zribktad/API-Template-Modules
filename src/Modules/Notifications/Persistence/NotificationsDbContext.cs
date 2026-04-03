using Microsoft.EntityFrameworkCore;
using Notifications.Shared;

using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.SoftDelete;

namespace Notifications.Persistence;

public sealed class NotificationsDbContext : ModuleDbContext
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
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

    public DbSet<FailedEmail> FailedEmails => Set<FailedEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}



