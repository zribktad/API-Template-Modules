using BackgroundJobs.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.SoftDelete;

namespace BackgroundJobs.Infrastructure.Persistence;

public sealed class BackgroundJobsDbContext : ModuleDbContext
{
    public BackgroundJobsDbContext(
        DbContextOptions<BackgroundJobsDbContext> options,
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

    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackgroundJobsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
