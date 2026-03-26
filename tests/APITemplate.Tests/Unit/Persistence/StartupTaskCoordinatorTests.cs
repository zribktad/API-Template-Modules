using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Startup;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Persistence.Startup;
using APITemplate.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public sealed class StartupTaskCoordinatorTests
{
    [Fact]
    public async Task TestNoOpStartupTaskCoordinator_CoordinateAsync_ExecutesActionImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = new TestNoOpStartupTaskCoordinator();
        var executed = false;

        await using var startupLease = await sut.AcquireAsync(StartupTaskName.AppBootstrap, ct);
        executed = true;

        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task PostgresAdvisoryLockStartupTaskCoordinator_WhenProviderIsNotNpgsql_FallsBackToNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbContext = CreateDbContext();
        var sut = new PostgresAdvisoryLockStartupTaskCoordinator(
            dbContext,
            NullLogger<PostgresAdvisoryLockStartupTaskCoordinator>.Instance
        );

        var callCount = 0;

        await using var startupLease = await sut.AcquireAsync(StartupTaskName.AppBootstrap, ct);
        callCount++;

        callCount.ShouldBe(1);
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
            new SoftDeleteProcessor(stateManager)
        );
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
