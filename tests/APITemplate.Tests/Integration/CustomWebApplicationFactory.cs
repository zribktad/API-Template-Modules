using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace APITemplate.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    // Pre-warm the server before parallel test classes start calling CreateClient().
    // Without this, concurrent constructors race to call StartServer(), causing duplicate
    // InMemory DB seed operations ("An item with the same key has already been added").
    public ValueTask InitializeAsync()
    {
        _ = Server;
        return ValueTask.CompletedTask;
    }

    // DisposeAsync() is satisfied by WebApplicationFactory<T>'s IAsyncDisposable implementation.

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configBuilder) =>
            {
                var config = TestConfigurationHelper.GetBaseConfiguration();
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
                options
                    .UseInMemoryDatabase(_dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            );

            TestServiceHelper.MockMongoServices(services);
            TestServiceHelper.RemoveExternalHealthChecks(services);
            TestServiceHelper.ReplaceProductRepositoryWithInMemory(services);
            TestServiceHelper.ReplaceOutputCacheWithInMemory(services);
            TestServiceHelper.ReplaceDataProtectionWithInMemory(services);
            TestServiceHelper.ReplaceTicketStoreWithInMemory(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
            TestServiceHelper.RemoveTickerQRuntimeServices(services);
            TestServiceHelper.ReplaceStartupCoordinationWithNoOp(services);
        });

        builder.UseEnvironment("Development");
    }
}
