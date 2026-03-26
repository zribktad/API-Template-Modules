using APITemplate.Application.Common.Context;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.StoredProcedures;

public sealed class StoredProcedureExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProviderDoesNotSupportRawSql_Throws()
    {
        await using var dbContext = CreateDbContext();
        var sut = new StoredProcedureExecutor(dbContext);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync($"select 1", TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var stateManager = new AuditableEntityStateManager();

        return new AppDbContext(
            options,
            new TestTenantProvider(),
            new TestActorProvider(),
            TimeProvider.System,
            [],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager));
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
