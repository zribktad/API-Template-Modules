using APITemplate.Tests.Unit.Helpers;
using BuildingBlocks.Infrastructure.EFCore.Auditing;
using Identity.Directory.Entities;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Exercises AppUser EF Core persistence after the S3 persistence-flattening refactor:
///     backing properties (DbEmail, DbNormalizedEmail, DbUsername, DbNormalizedUsername) must
///     round-trip correctly and unique indexes must be enforced at the database level.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Integration.Postgres")]
[Trait("Docker", "true")]
public sealed class AppUserPersistencePostgresTests
    : IClassFixture<SharedPostgresContainer>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset FixedUtc = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = null!;
    private Guid _tenantId;
    private IdentityDbContext _dbContext = null!;
    private readonly TimeProvider _time = new FakeTimeProvider(FixedUtc);

    public AppUserPersistencePostgresTests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"appuser_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );

        _tenantId = Guid.NewGuid();
        _dbContext = CreateDbContext();
        await _dbContext.Database.MigrateAsync(ct);

        Tenant tenant = Tenant.Create(
            _tenantId,
            "t" + _tenantId.ToString("N")[..12],
            "Persistence test tenant"
        );
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    // ── Round-trip persistence ─────────────────────────────────────────────

    [Fact]
    public async Task SaveAndReload_PreservesDbEmailAndDbNormalizedEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");

        await PersistUserAsync(user, ct);

        AppUser reloaded = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, ct);
        reloaded.DbEmail.ShouldBe("alice@example.com");
        reloaded.DbNormalizedEmail.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public async Task SaveAndReload_PreservesDbUsernameAndDbNormalizedUsername()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");

        await PersistUserAsync(user, ct);

        AppUser reloaded = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, ct);
        reloaded.DbUsername.ShouldBe("alice");
        reloaded.DbNormalizedUsername.ShouldBe("ALICE");
    }

    [Fact]
    public async Task SaveAndReload_EmailFacadeReturnsCorrectValues()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");

        await PersistUserAsync(user, ct);

        AppUser reloaded = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, ct);
        reloaded.Email.Value.ShouldBe("alice@example.com");
        reloaded.Email.Normalized.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public async Task SaveAndReload_UsernameFacadeReturnsCorrectValues()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");

        await PersistUserAsync(user, ct);

        AppUser reloaded = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, ct);
        reloaded.Username.Value.ShouldBe("alice");
        reloaded.Username.Normalized.ShouldBe("ALICE");
    }

    // ── Column names ───────────────────────────────────────────────────────

    [Fact]
    public async Task ColumnNames_DbEmailMapsToEmailColumn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("col-test@example.com", "coltest");
        await PersistUserAsync(user, ct);

        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT "Email", "NormalizedEmail", "Username", "NormalizedUsername"
            FROM "Users"
            WHERE "Id" = '{user.Id}'
            """;
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        bool hasRow = await reader.ReadAsync(ct);

        hasRow.ShouldBeTrue();
        reader.GetString(0).ShouldBe("col-test@example.com");
        reader.GetString(1).ShouldBe("COL-TEST@EXAMPLE.COM");
        reader.GetString(2).ShouldBe("coltest");
        reader.GetString(3).ShouldBe("COLTEST");
    }

    // ── EF Core LINQ queries using Db properties ───────────────────────────

    [Fact]
    public async Task Query_ByDbNormalizedEmail_FindsUser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");
        await PersistUserAsync(user, ct);

        AppUser? found = await _dbContext
            .Users.Where(u => u.DbNormalizedEmail == "ALICE@EXAMPLE.COM")
            .FirstOrDefaultAsync(ct);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Query_ByDbNormalizedUsername_FindsUser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice");
        await PersistUserAsync(user, ct);

        AppUser? found = await _dbContext
            .Users.Where(u => u.DbNormalizedUsername == "ALICE")
            .FirstOrDefaultAsync(ct);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Query_ByDbNormalizedUsernameContains_FindsUserBySubstring()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateUser("alice@example.com", "alice.wonderland");
        await PersistUserAsync(user, ct);

        List<AppUser> found = await _dbContext
            .Users.Where(u => u.DbNormalizedUsername.Contains("WONDER"))
            .ToListAsync(ct);

        found.ShouldContain(u => u.Id == user.Id);
    }

    // ── Unique index enforcement ───────────────────────────────────────────

    [Fact]
    public async Task UniqueIndex_DuplicateNormalizedEmailInSameTenant_ThrowsDbUpdateException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser first = CreateUser("alice@example.com", "alice");
        await PersistUserAsync(first, ct);

        AppUser duplicate = CreateUser("ALICE@EXAMPLE.COM", "alice2");

        _dbContext.Users.Add(duplicate);
        await Should.ThrowAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task UniqueIndex_DuplicateNormalizedUsernameInSameTenant_ThrowsDbUpdateException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser first = CreateUser("alice@example.com", "alice");
        await PersistUserAsync(first, ct);

        AppUser duplicate = CreateUser("bob@example.com", "ALICE");

        _dbContext.Users.Add(duplicate);
        await Should.ThrowAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task UniqueIndex_SameNormalizedEmailInDifferentTenants_Succeeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Guid otherTenantId = Guid.NewGuid();
        Tenant otherTenant = Tenant.Create(
            otherTenantId,
            "t" + otherTenantId.ToString("N")[..12],
            "Other tenant"
        );
        _dbContext.Tenants.Add(otherTenant);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.ChangeTracker.Clear();

        AppUser userInTenantA = CreateUser("alice@example.com", "alice");
        await PersistUserAsync(userInTenantA, ct);

        AppUser userInTenantB = CreateUser("alice@example.com", "alice", otherTenantId);
        _dbContext.Users.Add(userInTenantB);
        await _dbContext.SaveChangesAsync(ct);

        int total = await _dbContext
            .Users.IgnoreQueryFilters()
            .CountAsync(u => u.DbNormalizedEmail == "ALICE@EXAMPLE.COM", ct);
        total.ShouldBe(2);
    }

    private AppUser CreateUser(string email, string username, Guid? tenantId = null) =>
        AppUser.Create(username, email, keycloakUserId: null, tenantId ?? _tenantId);

    private async Task PersistUserAsync(AppUser user, CancellationToken ct)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.ChangeTracker.Clear();
    }

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_connectionString).Options;

        return new IdentityDbContext(
            options,
            new IdentityIntegrationTenantProvider(_tenantId),
            new IdentityIntegrationEmptyActorProvider(),
            _time,
            new AuditableEntityStateManager()
        );
    }
}
