using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace APITemplate.Tests.Integration;

/// <summary>
///     Like <see cref="CustomWebApplicationFactory" />, but removes any registered
///     <see cref="IConnectionMultiplexer" /> so the host matches a no-Redis configuration
///     (in-process BFF + memory distributed cache only).
/// </summary>
public sealed class EmptyRedisWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // appsettings.json ships a non-empty placeholder; force no Redis so Identity registers
        // PostgresDistributedCacheBffSessionStore + InProcessBffRefreshCoordinator (same as empty CS in prod).
        builder.UseSetting("Redis:ConnectionString", string.Empty);
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.RemoveAll<IConnectionMultiplexer>());
    }
}
