using APITemplate.Infrastructure.Persistence;
using Npgsql;
using Respawn;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

using AppDbUnitOfWork = SharedKernel.Infrastructure.UnitOfWork.UnitOfWork<APITemplate.Infrastructure.Persistence.AppDbContext>;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
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

    protected Task<AppDbContext> CreateDbContextAsync(
        bool hasTenant,
        Guid tenantId,
        Guid actorId,
        CancellationToken ct
    ) =>
        TestAppDbContextFactory.CreateAsync(
            _factory.ConnectionString,
            tenantId,
            actorId,
            hasTenant,
            ct
        );

    protected static AppDbUnitOfWork CreateUnitOfWork(AppDbContext dbContext) =>
        TestAppDbContextFactory.CreateUnitOfWork(dbContext);
}
