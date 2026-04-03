using SharedKernel.Application.Options.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.Utilities.Entities;

namespace BackgroundJobs.TickerQ;

public sealed class TickerQSchedulerDbContext : TickerQDbContext<TimeTickerEntity, CronTickerEntity>
{
    public TickerQSchedulerDbContext(DbContextOptions<TickerQSchedulerDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TickerQSchedulerOptions.DefaultSchemaName);
        base.OnModelCreating(modelBuilder);

        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetSchema(TickerQSchedulerOptions.DefaultSchemaName);
        }
    }
}


