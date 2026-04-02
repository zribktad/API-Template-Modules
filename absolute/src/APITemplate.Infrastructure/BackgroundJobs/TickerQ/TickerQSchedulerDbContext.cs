using APITemplate.Application.Common.Options;
using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.Utilities.Entities;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ;

/// <summary>
/// EF Core <see cref="DbContext"/> that hosts the TickerQ scheduler tables
/// (<c>TimeTickers</c> and <c>CronTickers</c>) in the dedicated TickerQ schema.
/// Used exclusively by TickerQ internals and the job registrar.
/// </summary>
public sealed class TickerQSchedulerDbContext : TickerQDbContext<TimeTickerEntity, CronTickerEntity>
{
    public TickerQSchedulerDbContext(DbContextOptions<TickerQSchedulerDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TickerQSchedulerOptions.DefaultSchemaName);
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetSchema(TickerQSchedulerOptions.DefaultSchemaName);
        }
    }
}
