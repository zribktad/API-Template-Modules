using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Composition;

/// <summary>
///     Verifies DI when Redis connection string is empty (see
///     <see cref="APITemplate.Tests.Integration.EmptyRedisWebApplicationFactory" />)
///     and no Redis multiplexer is registered.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public sealed class EmptyRedisHostTests : IClassFixture<EmptyRedisWebApplicationFactory>
{
    private readonly EmptyRedisWebApplicationFactory _factory;

    public EmptyRedisHostTests(EmptyRedisWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task HostStarts_AndResolvesInProcessBffServices()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();

        IBffSessionStore store = _factory.Services.GetRequiredService<IBffSessionStore>();
        store.ShouldBeOfType<CachingBffSessionStoreDecorator>();

        PostgresDistributedCacheBffSessionStore inner =
            _factory.Services.GetRequiredService<PostgresDistributedCacheBffSessionStore>();
        inner.ShouldNotBeNull();

        IBffRefreshCoordinator refresh =
            _factory.Services.GetRequiredService<IBffRefreshCoordinator>();
        refresh.ShouldBeOfType<InProcessBffRefreshCoordinator>();
    }
}
