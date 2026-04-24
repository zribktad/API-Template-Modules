using Identity.Auth.Entities;
using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Identity.Directory.Entities;
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
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class PostgresCachedBffSessionStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = BffSessionStoreUnitTestHelpers.DefaultSessionEpoch;

    private readonly IdentityDbContext _dbContext;
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly BffOptions _bffOptions = new() { CacheTtlMinutes = 10 };
    private readonly PostgresCachedBffSessionStore _sut;
    private readonly ServiceProvider _serviceProvider;

    public PostgresCachedBffSessionStoreTests()
    {
        string dbName = $"BffSessionStoreTests_{Guid.NewGuid():N}";

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

        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.IsConnected).Returns(false);

        _sut = new PostgresCachedBffSessionStore(
            _cache.Object,
            multiplexer.Object,
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

    // ── StoreAsync ──────────────────────────────────────────────────────────

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
        entity.UserId.ShouldBe(session.UserId);
        entity.Subject.ShouldBe(session.Subject);
        entity.Email.ShouldBe(session.Email);
        entity.DisplayName.ShouldBe(session.DisplayName);
        entity.Status.ShouldBe(BffSessionStatus.Active);
        entity.Version.ShouldBe(0);
    }

    [Fact]
    public async Task StoreAsync_EncryptsTokensInDatabase()
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
        entity.EncryptedAccessToken.ShouldNotBe(session.AccessToken);
        entity.EncryptedRefreshToken.ShouldNotBe(session.RefreshToken);
        entity.EncryptedAccessToken.ShouldNotBeNullOrWhiteSpace();
        entity.EncryptedRefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StoreAsync_WritesToRedisCache()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();

        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        _cache.Verify(
            x =>
                x.SetAsync(
                    $"bff:session:{session.SessionId}",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenCacheMiss_LoadsFromDatabaseAndPopulatesCache()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        // Reset cache mock so GetStringAsync returns null (cache miss)
        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? result = await _sut.GetAsync(
            session.SessionId,
            TestContext.Current.CancellationToken
        );

        result.ShouldNotBeNull();
        result.SessionId.ShouldBe(session.SessionId);
        result.AccessToken.ShouldBe(session.AccessToken);
        result.RefreshToken.ShouldBe(session.RefreshToken);

        // Should have written to cache after DB load
        _cache.Verify(
            x =>
                x.SetAsync(
                    $"bff:session:{session.SessionId}",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAsync_WhenNotFound_ReturnsNull()
    {
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? result = await _sut.GetAsync(
            "nonexistent-session",
            TestContext.Current.CancellationToken
        );

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_WhenSoftDeleted_ReturnsNull()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        // Soft-delete the entity
        BffPersistedSession entity = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstAsync(
                s => s.SessionId == session.SessionId,
                TestContext.Current.CancellationToken
            );
        entity.IsDeleted = true;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? result = await _sut.GetAsync(
            session.SessionId,
            TestContext.Current.CancellationToken
        );

        result.ShouldBeNull();
    }

    // ── TryUpdateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryUpdateAsync_WithMatchingVersion_UpdatesAndReturnsTrue()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        BffSessionRecord updated = session with
        {
            Email = "updated@example.com",
            LastSeenAtUtc = Now.AddMinutes(5),
            Version = 1,
        };

        bool result = await _sut.TryUpdateAsync(
            updated,
            expectedVersion: 0,
            TestContext.Current.CancellationToken
        );

        result.ShouldBeTrue();

        BffPersistedSession entity = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstAsync(
                s => s.SessionId == session.SessionId,
                TestContext.Current.CancellationToken
            );

        entity.Email.ShouldBe("updated@example.com");
        entity.Version.ShouldBe(1);
    }

    [Fact]
    public async Task TryUpdateAsync_WithWrongVersion_ReturnsFalse()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        BffSessionRecord updated = session with { Email = "updated@example.com", Version = 1 };

        bool result = await _sut.TryUpdateAsync(
            updated,
            expectedVersion: 999,
            TestContext.Current.CancellationToken
        );

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task TryUpdateAsync_WhenNotFound_ReturnsFalse()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession(
            sessionId: "nonexistent"
        );
        BffSessionRecord updated = session with { Email = "updated@example.com" };

        bool result = await _sut.TryUpdateAsync(
            updated,
            expectedVersion: 0,
            TestContext.Current.CancellationToken
        );

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task TryUpdateAsync_OnSuccess_UpdatesRedisCache()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);
        _cache.Reset();

        BffSessionRecord updated = session with { Email = "updated@example.com", Version = 1 };

        await _sut.TryUpdateAsync(
            updated,
            expectedVersion: 0,
            TestContext.Current.CancellationToken
        );

        _cache.Verify(
            x =>
                x.SetAsync(
                    $"bff:session:{session.SessionId}",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── RemoveAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_SoftDeletesInDatabase()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        await _sut.RemoveAsync(session.SessionId, TestContext.Current.CancellationToken);

        BffPersistedSession entity = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstAsync(
                s => s.SessionId == session.SessionId,
                TestContext.Current.CancellationToken
            );

        entity.IsDeleted.ShouldBeTrue();
        entity.DeletedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromRedisCache()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        await _sut.RemoveAsync(session.SessionId, TestContext.Current.CancellationToken);

        _cache.Verify(
            x => x.RemoveAsync($"bff:session:{session.SessionId}", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenNotFound_DoesNotThrow()
    {
        await Should.NotThrowAsync(() =>
            _sut.RemoveAsync("nonexistent", TestContext.Current.CancellationToken)
        );
    }

    // ── Roundtrip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAndGet_RoundtripsAllFields()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            IdToken = "id-token-value",
            TenantId = Guid.NewGuid().ToString(),
            Roles = ["Admin", "User"],
            RefreshTokenExpiresAtUtc = Now.AddDays(30),
        };

        await _sut.StoreAsync(session, TestContext.Current.CancellationToken);

        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? loaded = await _sut.GetAsync(
            session.SessionId,
            TestContext.Current.CancellationToken
        );

        loaded.ShouldNotBeNull();
        loaded.SessionId.ShouldBe(session.SessionId);
        loaded.UserId.ShouldBe(session.UserId);
        loaded.Subject.ShouldBe(session.Subject);
        loaded.Provider.ShouldBe(session.Provider);
        loaded.TenantId.ShouldBe(session.TenantId);
        loaded.Roles.ShouldBe(session.Roles);
        loaded.Email.ShouldBe(session.Email);
        loaded.DisplayName.ShouldBe(session.DisplayName);
        loaded.AccessToken.ShouldBe(session.AccessToken);
        loaded.RefreshToken.ShouldBe(session.RefreshToken);
        loaded.IdToken.ShouldBe(session.IdToken);
        loaded.AccessTokenExpiresAtUtc.ShouldBe(session.AccessTokenExpiresAtUtc);
        loaded.RefreshTokenExpiresAtUtc.ShouldBe(session.RefreshTokenExpiresAtUtc);
        loaded.CreatedAtUtc.ShouldBe(session.CreatedAtUtc);
        loaded.LastSeenAtUtc.ShouldBe(session.LastSeenAtUtc);
        loaded.LastRefreshedAtUtc.ShouldBe(session.LastRefreshedAtUtc);
        loaded.Status.ShouldBe(session.Status);
        loaded.Version.ShouldBe(session.Version);
    }
}
