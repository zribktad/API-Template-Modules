using System.Security.Claims;
using APITemplate.Tests.Unit.Helpers;
using Identity.Auth.Entities;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Identity.Directory.Entities;
using Identity.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

[Trait("Category", "Integration")]
[Trait("Category", "Integration.Postgres")]
[Trait("Docker", "true")]
public sealed class BffSessionServiceE2ETests
    : IClassFixture<SharedPostgresContainer>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-11T10:00:00Z");

    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = default!;
    private IdentityDbContext _dbContext = default!;
    private ServiceProvider _serviceProvider = default!;

    public BffSessionServiceE2ETests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"bffsession_e2e_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );

        ServiceCollection services = new();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<ITenantProvider>(new StubTenantProvider());
        services.AddSingleton<IActorProvider>(new IdentityIntegrationEmptyActorProvider());
        services.AddSingleton<IAuditableEntityStateManager>(new AuditableEntityStateManager());
        services.AddDistributedMemoryCache();
        services.AddSingleton(Options.Create(CreateBffOptions()));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IBffSessionTokenProtector>(
            new BffSessionTokenProtector(
                new EphemeralDataProtectionProvider(),
                NullLogger<BffSessionTokenProtector>.Instance
            )
        );
        services.AddSingleton<IBffSessionDbContextFactory, ScopedBffSessionDbContextFactory>();
        services.AddSingleton<PostgresDistributedCacheBffSessionStore>();
        services.AddSingleton<IBffLocalSessionCache, BffLocalSessionCache>();
        services.AddSingleton<IBffSessionRevocationNotifier, NullBffSessionRevocationNotifier>();
        services.AddSingleton<IBffSessionStore>(sp => new CachingBffSessionStoreDecorator(
            sp.GetRequiredService<PostgresDistributedCacheBffSessionStore>(),
            sp.GetRequiredService<IBffLocalSessionCache>(),
            sp.GetRequiredService<IBffSessionRevocationNotifier>(),
            NullLogger<CachingBffSessionStoreDecorator>.Instance
        ));
        services.AddSingleton<IBffSessionPrincipalFactory, BffSessionPrincipalFactory>();
        services.AddSingleton<IBffSessionRecordFactory, BffSessionRecordFactory>();
        services.AddSingleton<IBffSessionValidator, BffSessionValidator>();
        services.AddSingleton<IBffSessionMutator>(sp => new BffSessionMutator(
            sp.GetRequiredService<IBffSessionStore>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            NullLogger<BffSessionMutator>.Instance
        ));
        services.AddSingleton<BffSessionService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = CreateDbContext();
        await _dbContext.Database.MigrateAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task CreateGetUpdateRevoke_HappyPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionService service = _serviceProvider.GetRequiredService<BffSessionService>();

        string sessionId = await service.CreateSessionAsync(
            CreateTicket("access-1", "refresh-1"),
            ct
        );
        BffSessionRecord? loaded = await service.GetSessionAsync(sessionId, ct);
        loaded.ShouldNotBeNull();

        await service.UpdateSessionFromTicketAsync(
            sessionId,
            CreateTicket("access-2", "refresh-2"),
            ct
        );
        BffSessionRecord? updated = await service.GetSessionAsync(sessionId, ct);
        updated.ShouldNotBeNull();
        updated.AccessToken.ShouldBe("access-2");
        updated.Version.ShouldBeGreaterThan(loaded.Version);

        await service.RevokeAsync(sessionId, BffSessionRevocationReason.Logout, ct);

        BffSessionRecord? afterRevoke = await service.GetSessionAsync(sessionId, ct);
        afterRevoke.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ServedFromL1OnSecondCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffLocalSessionCache localCache = new(Options.Create(CreateBffOptions()));
        PostgresDistributedCacheBffSessionStore postgresStore =
            _serviceProvider.GetRequiredService<PostgresDistributedCacheBffSessionStore>();
        CountingSessionStore inner = new(postgresStore);
        CachingBffSessionStoreDecorator sut = new(
            inner,
            localCache,
            new NullBffSessionRevocationNotifier(),
            NullLogger<CachingBffSessionStoreDecorator>.Instance
        );
        BffSessionRecord session = CreateSession();
        await sut.StoreAsync(session, ct);
        localCache.Invalidate(session.SessionId);
        inner.GetCalls = 0;

        await sut.GetAsync(session.SessionId, ct);
        await sut.GetAsync(session.SessionId, ct);

        inner.GetCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentTryUpdate_OnlyOneWinsOptimisticLock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IBffSessionStore store = _serviceProvider.GetRequiredService<IBffSessionStore>();
        BffSessionRecord session = CreateSession();
        await store.StoreAsync(session, ct);

        Task<bool>[] updates = Enumerable
            .Range(0, 5)
            .Select(index =>
                store.TryUpdateAsync(
                    session with
                    {
                        Email = $"updated-{index}@example.com",
                        Version = session.Version + 1,
                    },
                    expectedVersion: session.Version,
                    ct
                )
            )
            .ToArray();

        bool[] results = await Task.WhenAll(updates);

        results.Count(result => result).ShouldBe(1);
        results.Count(result => !result).ShouldBe(4);
    }

    [Fact]
    public async Task BulkRevokeBySubject_AllSessionsTerminal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionService service = _serviceProvider.GetRequiredService<BffSessionService>();
        IBffSessionStore store = _serviceProvider.GetRequiredService<IBffSessionStore>();
        string subject = Guid.NewGuid().ToString("N");
        BffSessionRecord[] sessions =
        [
            CreateSession(subject: subject),
            CreateSession(subject: subject),
            CreateSession(subject: subject),
        ];
        foreach (BffSessionRecord session in sessions)
            await store.StoreAsync(session, ct);

        await service.RevokeAllSessionsForSubjectAsync(
            subject,
            BffSessionRevocationReason.CredentialRotation,
            ct
        );

        List<BffPersistedSession> revoked = await _dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(session => session.Subject == subject)
            .ToListAsync(ct);

        revoked.Count.ShouldBe(3);
        revoked.ShouldAllBe(session => session.Status == BffSessionStatus.Revoked);
        revoked.ShouldAllBe(session => session.RevokedAtUtc.HasValue);
        revoked.ShouldAllBe(session =>
            session.RevocationReason == BffSessionRevocationReason.CredentialRotation
        );
    }

    [Fact]
    public async Task GetAsync_WhenAbsoluteLifetimeExceeded_ValidatorExpires()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionService service = _serviceProvider.GetRequiredService<BffSessionService>();
        IBffSessionStore store = _serviceProvider.GetRequiredService<IBffSessionStore>();
        BffSessionRecord session = CreateSession() with { CreatedAtUtc = Now.AddHours(-9) };
        await store.StoreAsync(session, ct);

        BffSessionRecord? result = await service.GetSessionAsync(session.SessionId, ct);

        result.ShouldBeNull();
        BffPersistedSession? entity = await FindSessionAsync(session.SessionId, ct);
        entity.ShouldNotBeNull();
        entity.Status.ShouldBe(BffSessionStatus.Revoked);
        entity.RevocationReason.ShouldBe(BffSessionRevocationReason.AbsoluteLifetimeExceeded);
    }

    [Fact]
    public async Task GetAsync_WhenTerminal_ReturnsNullFromService()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionService service = _serviceProvider.GetRequiredService<BffSessionService>();
        IBffSessionStore store = _serviceProvider.GetRequiredService<IBffSessionStore>();
        BffSessionRecord session = CreateSession() with { Status = BffSessionStatus.Revoked };
        await store.StoreAsync(session, ct);

        BffSessionRecord? result = await service.GetSessionAsync(session.SessionId, ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CachingDecorator_InvalidateAfterUpdate_L1Stale_NextReadHitsDb()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffLocalSessionCache localCache = new(Options.Create(CreateBffOptions()));
        CountingSessionStore inner = new(
            _serviceProvider.GetRequiredService<PostgresDistributedCacheBffSessionStore>()
        );
        CachingBffSessionStoreDecorator sut = new(
            inner,
            localCache,
            new NullBffSessionRevocationNotifier(),
            NullLogger<CachingBffSessionStoreDecorator>.Instance
        );
        BffSessionRecord session = CreateSession();
        await sut.StoreAsync(session, ct);
        await sut.GetAsync(session.SessionId, ct);
        inner.GetCalls = 0;

        BffSessionRecord updated = session with
        {
            Email = "updated@example.com",
            Version = session.Version + 1,
        };
        await sut.TryUpdateAsync(updated, session.Version, ct);
        localCache.Invalidate(session.SessionId);
        await sut.GetAsync(session.SessionId, ct);

        inner.GetCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CachingDecorator_ReadThroughRacesInvalidate_DoesNotStorePopulate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffLocalSessionCache localCache = new(Options.Create(CreateBffOptions()));
        IBffSessionStore inner = new InvalidatingReadSessionStore(
            _serviceProvider.GetRequiredService<PostgresDistributedCacheBffSessionStore>(),
            localCache
        );
        CachingBffSessionStoreDecorator sut = new(
            inner,
            localCache,
            new NullBffSessionRevocationNotifier(),
            NullLogger<CachingBffSessionStoreDecorator>.Instance
        );
        BffSessionRecord session = CreateSession();
        await sut.StoreAsync(session, ct);
        localCache.Invalidate(session.SessionId);

        await sut.GetAsync(session.SessionId, ct);

        localCache.TryGet(session.SessionId, out _).ShouldBeFalse();
    }

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

    private Task<BffPersistedSession?> FindSessionAsync(string sessionId, CancellationToken ct) =>
        _dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.SessionId == sessionId, ct);

    private static BffOptions CreateBffOptions() =>
        new()
        {
            CacheTtlMinutes = 10,
            LocalCacheTtlSeconds = 60,
            LocalCacheMaxEntries = 128,
            SessionAbsoluteLifetimeMinutes = 480,
            SessionIdleTimeoutMinutes = 60,
        };

    private static AuthenticationTicket CreateTicket(string accessToken, string refreshToken)
    {
        ClaimsPrincipal principal = new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(AuthConstants.Claims.Subject, "sub-1"),
                    new Claim(ClaimTypes.Email, "user@example.com"),
                    new Claim(ClaimTypes.Name, "Test User"),
                ],
                AuthConstants.BffSchemes.Cookie
            )
        );

        AuthenticationProperties properties = new();
        properties.StoreTokens([
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = accessToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = refreshToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.ExpiresAt,
                Value = Now.AddMinutes(5).ToString("o"),
            },
        ]);

        return new AuthenticationTicket(principal, properties, AuthConstants.BffSchemes.Cookie);
    }

    private static BffSessionRecord CreateSession(string? subject = null) =>
        new()
        {
            SessionId = BffSessionIds.NewId(),
            UserId = Guid.NewGuid().ToString("N"),
            Subject = subject ?? Guid.NewGuid().ToString("N"),
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

    private class CountingSessionStore(IBffSessionStore inner) : IBffSessionStore
    {
        public int GetCalls { get; set; }

        public virtual async Task<BffSessionRecord?> GetAsync(
            string sessionId,
            CancellationToken ct = default
        )
        {
            GetCalls++;
            return await inner.GetAsync(sessionId, ct);
        }

        public Task StoreAsync(BffSessionRecord session, CancellationToken ct = default) =>
            inner.StoreAsync(session, ct);

        public Task<bool> TryUpdateAsync(
            BffSessionRecord session,
            long expectedVersion,
            CancellationToken ct = default
        ) => inner.TryUpdateAsync(session, expectedVersion, ct);

        public Task RemoveAsync(string sessionId, CancellationToken ct = default) =>
            inner.RemoveAsync(sessionId, ct);

        public Task<IReadOnlyList<string>> FindActiveSessionIdsBySubjectAsync(
            string keycloakSubject,
            CancellationToken ct = default
        ) => inner.FindActiveSessionIdsBySubjectAsync(keycloakSubject, ct);

        public Task<IReadOnlyList<string>> BulkRevokeActiveSessionsBySubjectAsync(
            string keycloakSubject,
            BffSessionRevocationReason reason,
            DateTimeOffset revokedAtUtc,
            CancellationToken ct = default
        ) =>
            inner.BulkRevokeActiveSessionsBySubjectAsync(keycloakSubject, reason, revokedAtUtc, ct);
    }

    private sealed class InvalidatingReadSessionStore(
        IBffSessionStore inner,
        IBffLocalSessionCache localCache
    ) : CountingSessionStore(inner)
    {
        public override async Task<BffSessionRecord?> GetAsync(
            string sessionId,
            CancellationToken ct = default
        )
        {
            BffSessionRecord? record = await base.GetAsync(sessionId, ct);
            localCache.Invalidate(sessionId);
            return record;
        }
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }
}
