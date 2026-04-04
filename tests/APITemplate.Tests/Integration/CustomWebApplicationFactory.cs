using APITemplate.Tests.Integration.Helpers;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace APITemplate.Tests.Integration;

/// <summary>
///     Hosts the modular API with real PostgreSQL and MongoDB via Testcontainers. Requires a local Docker engine.
///     Containers are created in <see cref="InitializeAsync" /> so fixture construction does not probe Docker.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private MongoDbContainer? _mongo;

    public async ValueTask InitializeAsync()
    {
        // Required so Program.cs can complete RunJasperFxCommands under WebApplicationFactory (see JasperFx docs).
        JasperFxEnvironment.AutoStartHost = true;

        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        _mongo = new MongoDbBuilder("mongo:7").WithCleanUp(true).Build();

        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync());

        // Pre-warm the host before parallel test classes race on StartServer().
        _ = Server;
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_mongo is not null)
            await _mongo.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        PostgreSqlContainer postgres =
            _postgres
            ?? throw new InvalidOperationException(
                "PostgreSQL container was not started; IAsyncLifetime.InitializeAsync must run first."
            );
        MongoDbContainer mongo =
            _mongo
            ?? throw new InvalidOperationException(
                "MongoDB container was not started; IAsyncLifetime.InitializeAsync must run first."
            );

        string pg = postgres.GetConnectionString();
        string mongoConnectionString = mongo.GetConnectionString();

        // Host settings are merged so WebApplication.CreateBuilder sees these before reading
        // ConnectionStrings:DefaultConnection (see Program.cs) — in-memory config alone can lose to appsettings.
        builder.UseSetting("ConnectionStrings:DefaultConnection", pg);
        builder.UseSetting("MongoDB:ConnectionString", mongoConnectionString);
        builder.UseSetting("MongoDB:DatabaseName", "apitemplate_integration");

        builder.ConfigureAppConfiguration(
            (_, configBuilder) =>
            {
                Dictionary<string, string?> config = TestConfigurationHelper.GetBaseConfiguration();
                config.Remove("ConnectionStrings:DefaultConnection");
                config.Remove("MongoDB:ConnectionString");
                config.Remove("MongoDB:DatabaseName");
                configBuilder.AddInMemoryCollection(config);
            }
        );

        builder.ConfigureTestServices(services =>
        {
            TestServiceHelper.RemoveExternalHealthChecks(services);
            TestServiceHelper.ReplaceOutputCacheWithInMemory(services);
            TestServiceHelper.ReplaceDataProtectionWithInMemory(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
            TestServiceHelper.RemoveTickerQRuntimeServices(services);
            TestServiceHelper.ReplaceStartupCoordinationWithNoOp(services);
        });

        builder.UseEnvironment("Development");
    }
}
