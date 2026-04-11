using APITemplate.Tests.Unit.Helpers;
using Identity.Entities;
using Identity.Handlers;
using Identity.Options;
using Identity.Persistence;
using Identity.Security.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel.Application.Context;
using SharedKernel.Contracts.Commands.Cleanup;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

[Trait("Category", "Integration.Docker")]
public sealed class BffSessionPostgresTests : IClassFixture<SharedPostgresContainer>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-10T12:00:00Z");

    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = default!;
    private IdentityDbContext _dbContext = default!;
    private PostgresCachedBffSessionStore _store = default!;
    private readonly Mock<IDistributedCache> _cache = new();

    public BffSessionPostgresTests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"bffsession_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );

        _dbContext = CreateDbContext();
        await _dbContext.Database.MigrateAsync(ct);

        // Build the store with real PG and mocked Redis
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped(_ => CreateDbContext());
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        Mock<IServiceScope> scope = new();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider);

        Mock<IServiceScopeFactory> scopeFactory = new();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        BffOptions bffOptions = new() { CacheTtlMinutes = 10 };
        IBffSessionTokenProtector tokenProtector = new BffSessionTokenProtector(
            new EphemeralDataProtectionProvider(),
            NullLogger<BffSessionTokenProtector>.Instance
        );

        Mock<IConnectionMultiplexer> multiplexer = new();
        multiplexer.Setup(m => m.IsConnected).Returns(false);

        _store = new PostgresCachedBffSessionStore(
            _cache.Object,
            multiplexer.Object,
            scopeFactory.Object,
            tokenProtector,
            Options.Create(bffOptions),
            TimeProvider.System
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    // ── Full lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_StoreGetUpdateRemove()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = CreateSession();

        // Store
        await _store.StoreAsync(session, ct);

        // Verify persisted to PG
        BffPersistedSession? entity = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, ct);
        entity.ShouldNotBeNull();
        entity.UserId.ShouldBe(session.UserId);
        entity.EncryptedAccessToken.ShouldNotBe(session.AccessToken);

        // Get (cache miss → loads from PG)
        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? loaded = await _store.GetAsync(session.SessionId, ct);
        loaded.ShouldNotBeNull();
        loaded.AccessToken.ShouldBe(session.AccessToken);
        loaded.RefreshToken.ShouldBe(session.RefreshToken);
        loaded.Email.ShouldBe(session.Email);

        // Update
        BffSessionRecord updated = loaded with
        {
            Email = "updated@example.com",
            LastSeenAtUtc = Now.AddMinutes(10),
            Version = loaded.Version + 1,
        };

        bool updateResult = await _store.TryUpdateAsync(updated, loaded.Version, ct);
        updateResult.ShouldBeTrue();

        // Verify update persisted
        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? reloaded = await _store.GetAsync(session.SessionId, ct);
        reloaded.ShouldNotBeNull();
        reloaded.Email.ShouldBe("updated@example.com");

        // Remove (soft-delete)
        await _store.RemoveAsync(session.SessionId, ct);

        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? afterRemove = await _store.GetAsync(session.SessionId, ct);
        afterRemove.ShouldBeNull();

        // Verify soft-deleted but still in DB
        BffPersistedSession? softDeleted = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, ct);
        softDeleted.ShouldNotBeNull();
        softDeleted.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task TryUpdateAsync_OptimisticConcurrencyConflict_ReturnsFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = CreateSession();
        await _store.StoreAsync(session, ct);

        // Attempt update with wrong version
        BffSessionRecord updated = session with
        {
            Email = "conflict@example.com",
            Version = 1,
        };

        bool result = await _store.TryUpdateAsync(updated, expectedVersion: 999, ct);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task StoreAsync_MultipleSessions_EachHasUniqueSessionId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        BffSessionRecord session1 = CreateSession(sessionId: "session-1");
        BffSessionRecord session2 = CreateSession(sessionId: "session-2");

        await _store.StoreAsync(session1, ct);
        await _store.StoreAsync(session2, ct);

        int count = await _dbContext.BffSessions.IgnoreQueryFilters().CountAsync(ct);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_DecryptsTokensCorrectly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord session = CreateSession() with
        {
            AccessToken = "super-secret-access-token-12345",
            RefreshToken = "super-secret-refresh-token-67890",
            IdToken = "super-secret-id-token-abcde",
        };

        await _store.StoreAsync(session, ct);

        _cache.Reset();
        _cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        BffSessionRecord? loaded = await _store.GetAsync(session.SessionId, ct);

        loaded.ShouldNotBeNull();
        loaded.AccessToken.ShouldBe("super-secret-access-token-12345");
        loaded.RefreshToken.ShouldBe("super-secret-refresh-token-67890");
        loaded.IdToken.ShouldBe("super-secret-id-token-abcde");
    }

    // ── Cleanup integration ──────────────────────────────────────────────────

    [Fact]
    public async Task CleanupHandler_DeletesExpiredSessionsFromRealPostgres()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Seed sessions directly into PG
        BffPersistedSession expiredSession = CreateEntity("expired-session");
        expiredSession.LastSeenAtUtc = Now.AddMinutes(-120);
        expiredSession.CreatedAtUtc = Now.AddMinutes(-120);
        expiredSession.RefreshTokenExpiresAtUtc = Now.AddMinutes(-10);

        BffPersistedSession activeSession = CreateEntity("active-session");
        activeSession.LastSeenAtUtc = Now.AddMinutes(-5);
        activeSession.CreatedAtUtc = Now.AddMinutes(-30);

        _dbContext.BffSessions.AddRange(expiredSession, activeSession);
        await _dbContext.SaveChangesAsync(ct);

        // Detach so the handler's query works cleanly
        _dbContext.ChangeTracker.Clear();

        BffOptions cleanupOptions = new()
        {
            SessionIdleTimeoutMinutes = 60,
            SessionAbsoluteLifetimeMinutes = 480,
        };

        FakeTimeProvider time = new(Now);

        await CleanupExpiredBffSessionsHandler.HandleAsync(
            new CleanupExpiredBffSessionsCommand(BatchSize: 100),
            _dbContext,
            Options.Create(cleanupOptions),
            time,
            NullLogger<CleanupExpiredBffSessionsHandler>.Instance,
            ct
        );

        List<BffPersistedSession> remaining = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .ToListAsync(ct);

        remaining.Count.ShouldBe(1);
        remaining[0].SessionId.ShouldBe("active-session");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_connectionString).Options;

        return new IdentityDbContext(
            options,
            new StubTenantProvider(),
            new IdentityIntegrationEmptyActorProvider(),
            TimeProvider.System,
            new AuditableEntityStateManager()
        );
    }

    private static BffSessionRecord CreateSession(string? sessionId = null)
    {
        return new BffSessionRecord
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
            UserId = Guid.NewGuid().ToString(),
            Subject = Guid.NewGuid().ToString(),
            Provider = BffProviderType.Keycloak,
            Email = "test@example.com",
            DisplayName = "Test User",
            AccessToken = "access-token-value",
            RefreshToken = "refresh-token-value",
            AccessTokenExpiresAtUtc = Now.AddMinutes(5),
            CreatedAtUtc = Now,
            LastSeenAtUtc = Now,
            LastRefreshedAtUtc = Now,
            Status = BffSessionStatus.Active,
            Version = 0,
        };
    }

    private static BffPersistedSession CreateEntity(string sessionId)
    {
        return new BffPersistedSession
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = Guid.NewGuid().ToString(),
            Subject = Guid.NewGuid().ToString(),
            Provider = BffProviderType.Keycloak,
            EncryptedAccessToken = "encrypted-access",
            EncryptedRefreshToken = "encrypted-refresh",
            AccessTokenExpiresAtUtc = Now.AddMinutes(5),
            CreatedAtUtc = Now,
            LastSeenAtUtc = Now,
            LastRefreshedAtUtc = Now,
            Status = BffSessionStatus.Active,
        };
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }
}
