using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.StoredProcedures;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.StoredProcedures;

[Trait("Category", "Unit")]
public sealed class StoredProcedureExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProviderDoesNotSupportRawSql_Throws()
    {
        await using TestEfContext dbContext = CreateDbContext();
        StoredProcedureExecutor sut = new(dbContext);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync($"select 1", TestContext.Current.CancellationToken)
        );
    }

    private static TestEfContext CreateDbContext()
    {
        DbContextOptions<TestEfContext> options = new DbContextOptionsBuilder<TestEfContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestEfContext(options);
    }

    private sealed class TestEfContext(DbContextOptions<TestEfContext> options)
        : DbContext(options);
}
