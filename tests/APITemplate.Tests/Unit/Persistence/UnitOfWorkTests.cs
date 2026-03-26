using System.Data;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class UnitOfWorkTests
{
    [Fact]
    public async Task CommitAsync_UsesExecutionStrategy_AndPersistsChanges()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var defaults = new TransactionDefaultsOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            TimeoutSeconds = 30,
            RetryEnabled = true,
            RetryCount = 6,
            RetryDelaySeconds = 8
        };
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, defaults, provider);

        dbContext.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            Name = "Committed"
        });

        await sut.CommitAsync(TestContext.Current.CancellationToken);

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        provider.CapturedOptions.ShouldBe(new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            TimeoutSeconds = 30,
            RetryEnabled = true,
            RetryCount = 6,
            RetryDelaySeconds = 8
        });
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithLegacyCallShape_UsesExecutionStrategy_AndPersistsChanges()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        await sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = "Books"
            });

            await Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        provider.BegunIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        provider.CapturedOptions.ShouldNotBeNull();
        provider.CapturedOptions.TimeoutSeconds.ShouldBe(30);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithPerCallOptions_MergesOverridesWithDefaults()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var defaults = new TransactionDefaultsOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            TimeoutSeconds = 30,
            RetryEnabled = true,
            RetryCount = 3,
            RetryDelaySeconds = 5
        };
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, defaults, provider);

        var result = await sut.ExecuteInTransactionAsync(
            async () =>
            {
                var category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "Games"
                };

                dbContext.Categories.Add(category);
                await Task.CompletedTask;
                return category.Id;
            },
            TestContext.Current.CancellationToken,
            new TransactionOptions
            {
                IsolationLevel = IsolationLevel.Serializable,
                TimeoutSeconds = 12,
                RetryEnabled = false
            });

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        provider.BegunIsolationLevel.ShouldBe(IsolationLevel.Serializable);
        provider.CapturedOptions.ShouldBe(new TransactionOptions
        {
            IsolationLevel = IsolationLevel.Serializable,
            TimeoutSeconds = 12,
            RetryEnabled = false,
            RetryCount = 3,
            RetryDelaySeconds = 5
        });
        (await dbContext.Categories.SingleAsync(c => c.Id == result, TestContext.Current.CancellationToken)).Name.ShouldBe("Games");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionThrows_RollsBackAndPropagates()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = "Music"
            });

            await Task.CompletedTask;
            throw new InvalidOperationException("boom");
        }, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(act);
        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        provider.LastCreatedTransaction!.RollbackCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenNestedFailureIsCaught_RollsBackToSavepoint_AndPersistsOuterChanges()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        await sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Outer-A" });

            try
            {
                await sut.ExecuteInTransactionAsync(async () =>
                {
                    dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Inner" });
                    await Task.CompletedTask;
                    throw new InvalidOperationException("inner failure");
                }, TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException)
            {
            }

            dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Outer-B" });
        }, TestContext.Current.CancellationToken);

        var transaction = provider.LastCreatedTransaction!;
        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        transaction.BeginCount.ShouldBe(1);
        transaction.CreateSavepointCount.ShouldBe(1);
        transaction.RollbackToSavepointCount.ShouldBe(1);
        transaction.CommitCount.ShouldBe(1);

        var categoryNames = await dbContext.Categories
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        categoryNames.ShouldBe(["Outer-A", "Outer-B"]);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenNestedGenericSucceeds_UsesSavepoint_AndReturnsValue()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var nestedResult = await sut.ExecuteInTransactionAsync(async () =>
        {
            return await sut.ExecuteInTransactionAsync(async () =>
            {
                var category = new Category { Id = Guid.NewGuid(), Name = "Nested" };
                dbContext.Categories.Add(category);
                await Task.CompletedTask;
                return category.Name;
            }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var transaction = provider.LastCreatedTransaction!;
        nestedResult.ShouldBe("Nested");
        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        transaction.CreateSavepointCount.ShouldBe(1);
        transaction.ReleaseSavepointCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenNestedOptionsConflict_Throws()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            await sut.ExecuteInTransactionAsync(
                async () =>
                {
                    await Task.CompletedTask;
                    return true;
                },
                TestContext.Current.CancellationToken,
                new TransactionOptions { IsolationLevel = IsolationLevel.Serializable });

            return true;
        }, TestContext.Current.CancellationToken);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("Nested transactions inherit");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenNestedFailureBubbles_RollsBackOuterTransaction()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Outer" });

            await sut.ExecuteInTransactionAsync(async () =>
            {
                dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Inner" });
                await Task.CompletedTask;
                throw new InvalidOperationException("nested bubble");
            }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(act);
        var transaction = provider.LastCreatedTransaction!;
        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        transaction.CreateSavepointCount.ShouldBe(1);
        transaction.RollbackToSavepointCount.ShouldBe(1);
        transaction.RollbackCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCommitIsCalledInsideOuterScope_ThrowsAndRollsBack()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Outer" });
            await sut.CommitAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);

        ex.Message.ShouldContain("CommitAsync cannot be called inside ExecuteInTransactionAsync");
        provider.LastCreatedTransaction!.RollbackCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenCommitIsCalledInsideNestedScope_ThrowsAndRollsBackOuterTransaction()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy);
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Outer" });

            await sut.ExecuteInTransactionAsync(async () =>
            {
                dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Inner" });
                await sut.CommitAsync(TestContext.Current.CancellationToken);
            }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);

        var transaction = provider.LastCreatedTransaction!;
        ex.Message.ShouldContain("CommitAsync cannot be called inside ExecuteInTransactionAsync");
        transaction.CreateSavepointCount.ShouldBe(1);
        transaction.RollbackToSavepointCount.ShouldBe(1);
        transaction.RollbackCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenProviderDoesNotSupportTransactions_SavesWithoutExplicitTransaction()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var provider = new RecordingTransactionProvider(executionStrategy)
        {
            BeginTransactionOverride = (_, _) =>
                throw new NotSupportedException("Transactions are not supported by this provider.")
        };
        var sut = CreateUnitOfWork(dbContext, new TransactionDefaultsOptions(), provider);

        await sut.ExecuteInTransactionAsync(
            async () =>
            {
                dbContext.Categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "NoTx"
                });

                await Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    private static UnitOfWork CreateUnitOfWork(
        AppDbContext dbContext,
        TransactionDefaultsOptions defaults,
        IDbTransactionProvider transactionProvider)
        => new(
            dbContext,
            Options.Create(defaults),
            NullLogger<UnitOfWork>.Instance,
            transactionProvider);

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
        public Guid TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public bool HasTenant => true;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }

    private sealed class RecordingTransactionProvider : IDbTransactionProvider
    {
        private readonly RecordingExecutionStrategy _executionStrategy;

        public RecordingTransactionProvider(RecordingExecutionStrategy executionStrategy)
        {
            _executionStrategy = executionStrategy;
        }

        public IDbContextTransaction? CurrentTransaction { get; set; }
        public TransactionOptions? CapturedOptions { get; private set; }
        public IsolationLevel? BegunIsolationLevel { get; private set; }
        public RecordingTransaction? LastCreatedTransaction { get; private set; }
        public Func<IsolationLevel, CancellationToken, Task<IDbContextTransaction>>? BeginTransactionOverride { get; set; }

        public IExecutionStrategy CreateExecutionStrategy(TransactionOptions options)
        {
            CapturedOptions = options;
            return _executionStrategy;
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct)
        {
            if (BeginTransactionOverride is not null)
                return BeginTransactionOverride(isolationLevel, ct);

            BegunIsolationLevel = isolationLevel;
            var transaction = new RecordingTransaction();
            LastCreatedTransaction = transaction;
            CurrentTransaction = transaction;
            return Task.FromResult<IDbContextTransaction>(transaction);
        }
    }

    private sealed class RecordingExecutionStrategy : IExecutionStrategy
    {
        public int ExecuteAsyncCallCount { get; private set; }
        public bool RetriesOnFailure => true;

        public void Execute(Action operation)
            => throw new NotSupportedException();

        public TResult Execute<TResult>(Func<TResult> operation)
            => throw new NotSupportedException();

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => throw new NotSupportedException();

        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncCallCount++;
            return operation(cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncCallCount++;
            return operation(cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncCallCount++;
            return operation(null!, state, cancellationToken);
        }
    }

    private sealed class RecordingTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public int BeginCount { get; set; } = 1;
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int CreateSavepointCount { get; private set; }
        public int RollbackToSavepointCount { get; private set; }
        public int ReleaseSavepointCount { get; private set; }

        public void Commit() => CommitCount++;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public void Rollback() => RollbackCount++;

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }

        public void CreateSavepoint(string name) => CreateSavepointCount++;

        public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateSavepointCount++;
            return Task.CompletedTask;
        }

        public void RollbackToSavepoint(string name) => RollbackToSavepointCount++;

        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            RollbackToSavepointCount++;
            return Task.CompletedTask;
        }

        public void ReleaseSavepoint(string name) => ReleaseSavepointCount++;

        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            ReleaseSavepointCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
