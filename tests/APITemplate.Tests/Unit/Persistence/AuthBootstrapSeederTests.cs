using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class AuthBootstrapSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenTenantExistsButInactiveOrDeleted_RestoresTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbContext = CreateDbContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Code = "default",
            Name = "Default Tenant",
            IsActive = false,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow,
            DeletedBy = Guid.NewGuid()
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(ct);

        var sut = CreateSeeder(dbContext);
        await sut.SeedAsync(ct);

        var restoredTenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Code == "default", ct);

        restoredTenant.IsActive.ShouldBeTrue();
        restoredTenant.IsDeleted.ShouldBeFalse();
        restoredTenant.DeletedAtUtc.ShouldBeNull();
        restoredTenant.DeletedBy.ShouldBeNull();
    }

    [Fact]
    public async Task SeedAsync_WhenNoTenantExists_CreatesTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbContext = CreateDbContext();

        var sut = CreateSeeder(dbContext);
        await sut.SeedAsync(ct);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Code == "default", ct);

        tenant.ShouldNotBeNull();
        tenant.IsActive.ShouldBeTrue();
        tenant.Name.ShouldBe("Default Tenant");
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

    private static AuthBootstrapSeeder CreateSeeder(AppDbContext dbContext)
    {
        var tenantOptions = Options.Create(new BootstrapTenantOptions
        {
            Code = "default",
            Name = "Default Tenant"
        });

        return new AuthBootstrapSeeder(dbContext, tenantOptions);
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
