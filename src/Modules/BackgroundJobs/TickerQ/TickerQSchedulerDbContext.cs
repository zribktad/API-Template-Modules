using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            entityType.SetSchema(TickerQSchedulerOptions.DefaultSchemaName);
    }
}
