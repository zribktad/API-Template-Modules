using APITemplate.Api.Extensions;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

[Trait("Category", "Integration.Postgres")]
public abstract class PostgresTestBase : IAsyncLifetime
{
    protected PostgresWebApplicationFactory _factory;
    protected HttpClient _client = default!;
    private Respawner? _respawner;

    protected PostgresTestBase(SharedPostgresContainer postgres)
    {
        _factory = new PostgresWebApplicationFactory(postgres.ServerConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    protected async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            await using var initConn = new NpgsqlConnection(_factory.ConnectionString);
            await initConn.OpenAsync();
            _respawner = await Respawner.CreateAsync(
                initConn,
                new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    TablesToIgnore =
                    [
                        new Respawn.Graph.Table("__EFMigrationsHistory"),
                        new Respawn.Graph.Table("Tenants"),
                    ],
                }
            );
        }

        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    protected async Task<AppDbContext> CreateDbContextAsync(
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
        var options = optionsBuilder.Options;

        var stateManager = new AuditableEntityStateManager();
        var context = new AppDbContext(
            options,
            new TestTenantProvider(tenantId, hasTenant),
            new TestActorProvider(actorId),
            TimeProvider.System,
            [new ProductSoftDeleteCascadeRule()],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager)
        );

        await context.Database.OpenConnectionAsync(ct);
        return context;
    }

    protected static UnitOfWork CreateUnitOfWork(AppDbContext dbContext) =>
        new(
            dbContext,
            Options.Create(new TransactionDefaultsOptions()),
            NullLogger<UnitOfWork>.Instance,
            new EfCoreTransactionProvider(dbContext)
        );

    protected sealed class TestTenantProvider(Guid tenantId, bool hasTenant) : ITenantProvider
    {
        public Guid TenantId => tenantId;
        public bool HasTenant => hasTenant;
    }

    protected sealed class TestActorProvider(Guid actorId) : IActorProvider
    {
        public Guid ActorId => actorId;
    }
}
