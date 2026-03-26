using APITemplate.Api.Extensions;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresTenantSoftDeleteCascadeTests(SharedPostgresContainer postgres)
    : PostgresTestBase(postgres)
{
    [Fact]
    public async Task DeleteTenant_SoftDeletesCascadesToUsersProductsAndCategories()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Code = $"tnt-cascade-{Guid.NewGuid():N}",
            Name = "Tenant Cascade Test",
        };
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"user-tnt-{Guid.NewGuid():N}",
            Email = $"tnt-{Guid.NewGuid():N}@example.com",
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Category-Tnt-{Guid.NewGuid():N}",
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Product-Tnt-{Guid.NewGuid():N}",
            Price = 100m,
            CategoryId = category.Id,
        };

        await using (
            var seedContext = await CreateTenantCascadeDbContextAsync(
                false,
                Guid.Empty,
                actorId,
                ct
            )
        )
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Users.Add(user);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (
            var deleteContext = await CreateTenantCascadeDbContextAsync(
                false,
                Guid.Empty,
                actorId,
                ct
            )
        )
        {
            var repository = new TenantRepository(deleteContext);
            var unitOfWork = CreateUnitOfWork(deleteContext);

            await repository.DeleteAsync(tenantId, ct);
            await unitOfWork.CommitAsync(ct);
        }

        await using var verifyContext = await CreateTenantCascadeDbContextAsync(
            false,
            Guid.Empty,
            actorId,
            ct
        );

        var deletedTenant = await verifyContext
            .Tenants.IgnoreQueryFilters()
            .SingleAsync(t => t.Id == tenantId, ct);
        deletedTenant.IsDeleted.ShouldBeTrue();
        deletedTenant.DeletedAtUtc.ShouldNotBeNull();
        deletedTenant.DeletedBy.ShouldBe(actorId);

        var deletedUser = await verifyContext
            .Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Id == user.Id, ct);
        deletedUser.IsDeleted.ShouldBeTrue();
        deletedUser.DeletedAtUtc.ShouldNotBeNull();
        deletedUser.DeletedBy.ShouldBe(actorId);

        var deletedCategory = await verifyContext
            .Categories.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == category.Id, ct);
        deletedCategory.IsDeleted.ShouldBeTrue();
        deletedCategory.DeletedAtUtc.ShouldNotBeNull();
        deletedCategory.DeletedBy.ShouldBe(actorId);

        var deletedProduct = await verifyContext
            .Products.IgnoreQueryFilters()
            .SingleAsync(p => p.Id == product.Id, ct);
        deletedProduct.IsDeleted.ShouldBeTrue();
        deletedProduct.DeletedAtUtc.ShouldNotBeNull();
        deletedProduct.DeletedBy.ShouldBe(actorId);
    }

    [Fact]
    public async Task DeleteTenant_DoesNotAffectOtherTenantEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();

        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        var tenantA = new Tenant
        {
            Id = tenantAId,
            TenantId = tenantAId,
            Code = $"tnt-a-{Guid.NewGuid():N}",
            Name = "Tenant A",
        };
        var tenantB = new Tenant
        {
            Id = tenantBId,
            TenantId = tenantBId,
            Code = $"tnt-b-{Guid.NewGuid():N}",
            Name = "Tenant B",
        };
        var categoryA = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantAId,
            Name = $"Cat-A-{Guid.NewGuid():N}",
        };
        var categoryB = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantBId,
            Name = $"Cat-B-{Guid.NewGuid():N}",
        };

        await using (
            var seedContext = await CreateTenantCascadeDbContextAsync(
                false,
                Guid.Empty,
                actorId,
                ct
            )
        )
        {
            seedContext.Tenants.AddRange(tenantA, tenantB);
            seedContext.Categories.AddRange(categoryA, categoryB);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (
            var deleteContext = await CreateTenantCascadeDbContextAsync(
                false,
                Guid.Empty,
                actorId,
                ct
            )
        )
        {
            var repository = new TenantRepository(deleteContext);
            var unitOfWork = CreateUnitOfWork(deleteContext);

            await repository.DeleteAsync(tenantAId, ct);
            await unitOfWork.CommitAsync(ct);
        }

        await using var verifyContext = await CreateTenantCascadeDbContextAsync(
            false,
            Guid.Empty,
            actorId,
            ct
        );

        var catA = await verifyContext
            .Categories.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == categoryA.Id, ct);
        catA.IsDeleted.ShouldBeTrue();

        var catB = await verifyContext
            .Categories.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == categoryB.Id, ct);
        catB.IsDeleted.ShouldBeFalse();

        var tB = await verifyContext
            .Tenants.IgnoreQueryFilters()
            .SingleAsync(t => t.Id == tenantBId, ct);
        tB.IsDeleted.ShouldBeFalse();
    }

    private async Task<AppDbContext> CreateTenantCascadeDbContextAsync(
        bool hasTenant,
        Guid tenantId,
        Guid actorId,
        CancellationToken ct
    )
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceServiceCollectionExtensions.ConfigurePostgresDbContext(
            optionsBuilder,
            _factory.ConnectionString
        );

        var stateManager = new AuditableEntityStateManager();
        var context = new AppDbContext(
            optionsBuilder.Options,
            new TestTenantProvider(tenantId, hasTenant),
            new TestActorProvider(actorId),
            TimeProvider.System,
            [new TenantSoftDeleteCascadeRule(), new ProductSoftDeleteCascadeRule()],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager)
        );

        await context.Database.OpenConnectionAsync(ct);
        return context;
    }
}
