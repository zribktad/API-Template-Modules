using Identity.Auth.Entities;
using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Identity.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

/// <summary>
///     Covers <see cref="PostgresDistributedCacheBffSessionStore" /> (IDistributedCache only, no multiplexer).
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresDistributedCacheBffSessionStoreTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly BffOptions _bffOptions = new() { CacheTtlMinutes = 10 };
    private readonly PostgresDistributedCacheBffSessionStore _sut;
    private readonly ServiceProvider _serviceProvider;

    public PostgresDistributedCacheBffSessionStoreTests()
    {
        string dbName = $"BffDistCacheStoreTests_{Guid.NewGuid():N}";

        ServiceCollection serviceCollection = new();
        serviceCollection.AddDbContext<IdentityDbContext>(
            (sp, opts) => opts.UseInMemoryDatabase(dbName),
            ServiceLifetime.Scoped
        );
        serviceCollection.AddSingleton<ITenantProvider>(
            new BffSessionStoreUnitTestHelpers.StubTenantProvider()
        );
        serviceCollection.AddSingleton<IActorProvider>(
            new BffSessionStoreUnitTestHelpers.StubActorProvider()
        );
        serviceCollection.AddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IAuditableEntityStateManager>(
            new AuditableEntityStateManager()
        );

        _serviceProvider = serviceCollection.BuildServiceProvider();

        IServiceScope assertionScope = _serviceProvider.CreateScope();
        _dbContext = assertionScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        IServiceScopeFactory scopeFactory =
            _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        IBffSessionTokenProtector tokenProtector = new BffSessionTokenProtector(
            new EphemeralDataProtectionProvider(),
            NullLogger<BffSessionTokenProtector>.Instance
        );

        _sut = new PostgresDistributedCacheBffSessionStore(
            _cache.Object,
            scopeFactory,
            tokenProtector,
            Options.Create(_bffOptions),
            TimeProvider.System
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task StoreAsync_PersistsSessionToDatabase()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();

        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        BffPersistedSession? entity = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.SessionId == session.SessionId,
                TestContext.Current.CancellationToken
            );
        entity.ShouldNotBeNull();
        entity!.SessionId.ShouldBe(session.SessionId);
    }
}
