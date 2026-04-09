using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.Persistence;

namespace BackgroundJobs.Persistence;

public sealed class BackgroundJobsDbContext : ModuleDbContext
{
    public BackgroundJobsDbContext(
        DbContextOptions<BackgroundJobsDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackgroundJobsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
