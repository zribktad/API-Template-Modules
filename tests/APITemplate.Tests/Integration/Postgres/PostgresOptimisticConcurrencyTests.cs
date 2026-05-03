using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Entities;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Verifies that PostgreSQL xmin-based optimistic concurrency is working correctly.
///     When two sessions load the same entity and both try to update it, the second save
///     must fail with a <see cref="DbUpdateConcurrencyException" />.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Integration.Postgres")]
[Trait("Docker", "true")]
public sealed class PostgresOptimisticConcurrencyTests
    : IClassFixture<SharedPostgresContainer>,
        IAsyncLifetime
{
    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = null!;
    private Guid _tenantId;
    private readonly TimeProvider _time = new FakeTimeProvider(
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
    );

    public PostgresOptimisticConcurrencyTests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"concurrency_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );

        _tenantId = Guid.NewGuid();
        using IdentityDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(ct);

        Tenant tenant = Tenant.Create(
            _tenantId,
            "t" + _tenantId.ToString("N")[..12],
            "Concurrency test tenant"
        );
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Update_WithConcurrentChange_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        AppUser user = AppUser.Create("alice", "alice@example.com", null, _tenantId);
        user.Id = userId;

        // 1. Create the user in the database
        using (IdentityDbContext dbContext = CreateDbContext())
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);
        }

        // 2. Open two independent sessions and load the same user
        using IdentityDbContext session1 = CreateDbContext();
        using IdentityDbContext session2 = CreateDbContext();

        AppUser user1 = await session1.Users.SingleAsync(u => u.Id == userId, ct);
        AppUser user2 = await session2.Users.SingleAsync(u => u.Id == userId, ct);

        uint xmin1Before = session1.Entry(user1).Property<uint>("xmin").CurrentValue;
        uint xmin2Before = session2.Entry(user2).Property<uint>("xmin").CurrentValue;

        xmin1Before.ShouldNotBe(0u);
        xmin2Before.ShouldNotBe(0u);
        xmin1Before.ShouldBe(xmin2Before);

        // 3. Modify session 1 and save.
        // This will update the row in Postgres and change its hidden 'xmin' value.
        user1.IsActive = false;
        await session1.SaveChangesAsync(ct);

        uint xmin1After = session1.Entry(user1).Property<uint>("xmin").CurrentValue;
        xmin1After.ShouldNotBe(xmin1Before);

        // 4. Modify session 2 and try to save.
        // We must change a property to trigger an UPDATE statement.
        // Even if we set it to the same value session 1 did, it's a change relative to session 2's original state.
        user2.IsActive = false;

        await Should.ThrowAsync<DbUpdateConcurrencyException>(async () =>
            await session2.SaveChangesAsync(ct)
        );
    }

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(_connectionString)
                .LogTo(
                    Console.WriteLine,
                    [DbLoggerCategory.Database.Command.Name],
                    LogLevel.Information
                )
                .Options;

        return new IdentityDbContext(
            options,
            new IdentityIntegrationTenantProvider(_tenantId),
            new IdentityIntegrationEmptyActorProvider(),
            _time,
            new AuditableEntityStateManager()
        );
    }
}
