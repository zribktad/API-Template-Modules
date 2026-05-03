using APITemplate.Api.Extensions;
using BuildingBlocks.Application.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
public sealed class InfrastructureConfigurationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InfrastructureConfigurationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void HstsConfiguration_WithInvalidMaxAge_ShouldFailValidation()
    {
        // Arrange & Act
        var act = () =>
            _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Hsts:MaxAgeDays", "0"); // Invalid: Range(1, int.MaxValue)
                })
                .CreateClient();

        // Assert
        // The error happens during host build/start due to ValidateOnStart()
        var exception = Should.Throw<OptionsValidationException>(() => act());
        exception.Message.ShouldContain("MaxAgeDays");
    }

    [Fact]
    public void RedisInfrastructure_WhenNotConfigured_ShouldRegisterMemoryCache()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" }
            )
            .Build();

        // Add Logging as it's used in the extension
        services.AddLogging();

        // Act
        services.AddRedisInfrastructure(configuration);

        // Assert
        services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer)).ShouldBeFalse();
        services.Any(d => d.ServiceType == typeof(IDistributedCache)).ShouldBeTrue();
    }

    [Fact]
    public void RedisInfrastructure_WhenConfigured_ShouldRegisterMultiplexerAsLazy()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Redis:ConnectionString"] = "localhost:6379" }
            )
            .Build();

        // Act
        services.AddRedisInfrastructure(configuration);
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IConnectionMultiplexer)
        );

        // Assert
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationFactory.ShouldNotBeNull(); // Verifies it's a factory, not an instance
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void BuildRedisConfigurationOptions_WhenMissingConnectionString_ShouldReturnDefaultConfig()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" }
            )
            .Build();

        // Act
        var options = configuration.BuildRedisConfigurationOptions();

        // Assert
        options.ShouldNotBeNull();
        options.AbortOnConnectFail.ShouldBeFalse();
    }
}
