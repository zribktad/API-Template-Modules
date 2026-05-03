using BuildingBlocks.Application.Context;
using BuildingBlocks.Infrastructure.EFCore.Auditing;
using BuildingBlocks.Infrastructure.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;

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
