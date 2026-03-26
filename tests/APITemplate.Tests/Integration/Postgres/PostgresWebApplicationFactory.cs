using APITemplate.Api.Extensions;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _serverConnectionString;
    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    public PostgresWebApplicationFactory(string serverConnectionString)
    {
        _serverConnectionString = serverConnectionString;
    }

    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_serverConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await cmd.ExecuteNonQueryAsync();

        // Pre-warm: trigger host build so EF migrations run before tests execute.
        _ = Services;
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        try
        {
            await using var conn = new NpgsqlConnection(_serverConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_databaseName}' AND pid <> pg_backend_pid();
                DROP DATABASE IF EXISTS "{_databaseName}";
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup — container disposal handles the rest.
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = ConnectionString;

        builder.ConfigureAppConfiguration(
            (_, configBuilder) =>
            {
                var config = TestConfigurationHelper.GetBaseConfiguration(
                    "APITemplate.Tests.RedactionKey.Postgres"
                );
                config["ConnectionStrings:DefaultConnection"] = connectionString;
                configBuilder.AddInMemoryCollection(config);
            }
        );

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            var optionsConfigs = services
                .Where(d =>
                    d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition()
                        .FullName?.Contains("IDbContextOptionsConfiguration") == true
                )
                .ToList();

            foreach (var d in optionsConfigs)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                PersistenceServiceCollectionExtensions.ConfigurePostgresDbContext(
                    options,
                    connectionString
                )
            );

            TestServiceHelper.RemoveExternalHealthChecks(services);

            services
                .AddHealthChecks()
                .AddNpgSql(connectionString, name: HealthCheckNames.PostgreSql, tags: ["database"]);

            TestServiceHelper.MockMongoServices(services);
            TestServiceHelper.ReplaceOutputCacheWithInMemory(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
            TestServiceHelper.RemoveTickerQRuntimeServices(services);
            TestServiceHelper.ReplaceStartupCoordinationWithNoOp(services);
        });

        builder.UseEnvironment("Development");
    }
}
