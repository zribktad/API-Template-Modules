using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class SharedPostgresContainer : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithCleanUp(true)
        .Build();

    public string ServerConnectionString
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
            {
                Database = "postgres"
            };
            return builder.ConnectionString;
        }
    }

    public ValueTask InitializeAsync() => new(Container.StartAsync());

    public async ValueTask DisposeAsync() => await Container.DisposeAsync();
}
