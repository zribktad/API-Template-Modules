using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Features.ProductReview;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresTransactionTests(SharedPostgresContainer postgres)
    : PostgresTestBase(postgres)
{
    [Fact]
    public async Task XminConcurrency_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"xmin-test-{Guid.NewGuid():N}";
        var (_, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            ct: ct
        );

        await using var scope1 = _factory.Services.CreateAsyncScope();
        await using var scope2 = _factory.Services.CreateAsyncScope();

        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity1 = await db1.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id, ct);

        var entity2 = await db2.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id, ct);

        entity1.Email = $"{username}.first@example.com";
        await db1.SaveChangesAsync(ct);

        entity2.Email = $"{username}.second@example.com";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task ProductReviewCreate_WhenRepositoryThrowsAfterTracking_RollsBackTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-tx-{Guid.NewGuid():N}",
            Name = "Tenant Transaction",
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Category-Tx-{Guid.NewGuid():N}",
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Product-Tx-{Guid.NewGuid():N}",
            Price = 99m,
            CategoryId = category.Id,
        };

        var user = new AppUser
        {
            Id = actorId,
            TenantId = tenantId,
            Username = $"tx-user-{Guid.NewGuid():N}",
            Email = $"tx-user-{Guid.NewGuid():N}@example.com",
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Users.Add(user);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            await seedContext.SaveChangesAsync(ct);
        }

        var expectedMessage = $"forced-after-add-{Guid.NewGuid():N}";

        await using (
            var transactionContext = await CreateDbContextAsync(true, tenantId, actorId, ct)
        )
        {
            var productRepository = new ProductRepository(transactionContext);
            var failingReviewRepository = new Mock<IProductReviewRepository>();
            failingReviewRepository
                .Setup(repository =>
                    repository.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>())
                )
                .Returns(
                    async (ProductReview entity, CancellationToken token) =>
                    {
                        transactionContext.ProductReviews.Add(entity);
                        await transactionContext.SaveChangesAsync(token);
                        throw new InvalidOperationException(expectedMessage);
                    }
                );
            var unitOfWork = CreateUnitOfWork(transactionContext);

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                CreateProductReviewCommandHandler.HandleAsync(
                    new CreateProductReviewCommand(
                        new CreateProductReviewRequest(product.Id, "rollback", 4)
                    ),
                    failingReviewRepository.Object,
                    productRepository,
                    unitOfWork,
                    new TestActorProvider(actorId),
                    Mock.Of<IMessageBus>(),
                    ct
                )
            );

            ex.Message.ShouldBe(expectedMessage);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var storedReviews = await verifyContext
            .ProductReviews.IgnoreQueryFilters()
            .Where(r => r.ProductId == product.Id)
            .ToListAsync(ct);

        storedReviews.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnitOfWork_WhenNestedTransactionFailsAndIsCaught_RollsBackInnerWorkToSavepoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-savepoint-{Guid.NewGuid():N}",
            Name = "Tenant Savepoint",
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        Guid outerCategoryAId = Guid.NewGuid();
        Guid outerCategoryBId = Guid.NewGuid();
        Guid innerCategoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    dbContext.Categories.Add(
                        new Category
                        {
                            Id = outerCategoryAId,
                            TenantId = tenantId,
                            Name = $"Outer-A-{Guid.NewGuid():N}",
                        }
                    );

                    try
                    {
                        await unitOfWork.ExecuteInTransactionAsync(
                            async () =>
                            {
                                dbContext.Categories.Add(
                                    new Category
                                    {
                                        Id = innerCategoryId,
                                        TenantId = tenantId,
                                        Name = $"Inner-{Guid.NewGuid():N}",
                                    }
                                );

                                await Task.CompletedTask;
                                throw new InvalidOperationException("force nested rollback");
                            },
                            ct
                        );
                    }
                    catch (InvalidOperationException ex)
                    {
                        ex.Message.ShouldBe("force nested rollback");
                    }

                    dbContext.Categories.Add(
                        new Category
                        {
                            Id = outerCategoryBId,
                            TenantId = tenantId,
                            Name = $"Outer-B-{Guid.NewGuid():N}",
                        }
                    );
                },
                ct
            );
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var storedCategoryIds = await verifyContext
            .Categories.IgnoreQueryFilters()
            .Where(c =>
                c.Id == outerCategoryAId || c.Id == outerCategoryBId || c.Id == innerCategoryId
            )
            .Select(c => c.Id)
            .ToListAsync(ct);

        storedCategoryIds.ShouldContain(outerCategoryAId);
        storedCategoryIds.ShouldContain(outerCategoryBId);
        storedCategoryIds.ShouldNotContain(innerCategoryId);
    }

    [Fact]
    public async Task UnitOfWork_WhenTransactionalWriteThrows_RollsBackAllStagedEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-full-rollback-{Guid.NewGuid():N}",
            Name = "Tenant Full Rollback",
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var act = () =>
                unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        dbContext.Categories.Add(
                            new Category
                            {
                                Id = categoryId,
                                TenantId = tenantId,
                                Name = $"Rollback-Category-{Guid.NewGuid():N}",
                            }
                        );

                        dbContext.Products.Add(
                            new Product
                            {
                                Id = productId,
                                TenantId = tenantId,
                                Name = $"Rollback-Product-{Guid.NewGuid():N}",
                                Price = 10m,
                                CategoryId = categoryId,
                            }
                        );

                        await Task.CompletedTask;
                        throw new InvalidOperationException("force outer rollback");
                    },
                    ct
                );

            await Should.ThrowAsync<InvalidOperationException>(act);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (
            await verifyContext
                .Categories.IgnoreQueryFilters()
                .CountAsync(c => c.Id == categoryId, ct)
        ).ShouldBe(0);
        (
            await verifyContext.Products.IgnoreQueryFilters().CountAsync(p => p.Id == productId, ct)
        ).ShouldBe(0);
    }

    [Fact]
    public async Task UnitOfWork_WithPerCallTransactionOptions_CommitsSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-options-{Guid.NewGuid():N}",
            Name = "Tenant Options",
        };
        var categoryId = Guid.NewGuid();

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    dbContext.Categories.Add(
                        new Category
                        {
                            Id = categoryId,
                            TenantId = tenantId,
                            Name = $"Options-Category-{Guid.NewGuid():N}",
                        }
                    );

                    await Task.CompletedTask;
                },
                ct,
                new TransactionOptions
                {
                    IsolationLevel = System.Data.IsolationLevel.Serializable,
                    TimeoutSeconds = 15,
                    RetryEnabled = false,
                }
            );
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (
            await verifyContext
                .Categories.IgnoreQueryFilters()
                .CountAsync(c => c.Id == categoryId, ct)
        ).ShouldBe(1);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitIsCalledInsideOuterTransaction_ThrowsAndRollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-commit-outer-{Guid.NewGuid():N}",
            Name = "Tenant Commit Outer",
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        var categoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        dbContext.Categories.Add(
                            new Category
                            {
                                Id = categoryId,
                                TenantId = tenantId,
                                Name = $"Commit-Outer-{Guid.NewGuid():N}",
                            }
                        );

                        await unitOfWork.CommitAsync(ct);
                    },
                    ct
                )
            );

            ex.Message.ShouldContain(
                "CommitAsync cannot be called inside ExecuteInTransactionAsync"
            );
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (
            await verifyContext
                .Categories.IgnoreQueryFilters()
                .CountAsync(c => c.Id == categoryId, ct)
        ).ShouldBe(0);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitIsCalledInsideNestedTransaction_ThrowsAndRollsBackOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-commit-inner-{Guid.NewGuid():N}",
            Name = "Tenant Commit Inner",
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        var outerCategoryId = Guid.NewGuid();
        var innerCategoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        dbContext.Categories.Add(
                            new Category
                            {
                                Id = outerCategoryId,
                                TenantId = tenantId,
                                Name = $"Commit-Outer-{Guid.NewGuid():N}",
                            }
                        );

                        await unitOfWork.ExecuteInTransactionAsync(
                            async () =>
                            {
                                dbContext.Categories.Add(
                                    new Category
                                    {
                                        Id = innerCategoryId,
                                        TenantId = tenantId,
                                        Name = $"Commit-Inner-{Guid.NewGuid():N}",
                                    }
                                );

                                await unitOfWork.CommitAsync(ct);
                            },
                            ct
                        );
                    },
                    ct
                )
            );

            ex.Message.ShouldContain(
                "CommitAsync cannot be called inside ExecuteInTransactionAsync"
            );
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (
            await verifyContext
                .Categories.IgnoreQueryFilters()
                .CountAsync(c => c.Id == outerCategoryId, ct)
        ).ShouldBe(0);
        (
            await verifyContext
                .Categories.IgnoreQueryFilters()
                .CountAsync(c => c.Id == innerCategoryId, ct)
        ).ShouldBe(0);
    }
}
