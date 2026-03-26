using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class TickerQSchedulerDbContextTests
{
    [Fact]
    public void Model_UsesDefaultTickerQSchemaForAllEntities()
    {
        var options = new DbContextOptionsBuilder<TickerQSchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using var dbContext = new TickerQSchedulerDbContext(options);

        dbContext.Model.GetDefaultSchema().ShouldBe("tickerq");
        dbContext
            .Model.GetEntityTypes()
            .Select(x => x.GetSchema())
            .Distinct()
            .ShouldBe(["tickerq"]);
    }
}
