using Microsoft.Extensions.Configuration;
using SharedKernel.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Configuration;

public sealed class RedisConfigurationExtensionsTests
{
    [Fact]
    public void IsRedisConfigured_WhenSectionMissing_ReturnsFalse()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        configuration.IsRedisConfigured().ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsRedisConfigured_WhenEmptyOrWhitespace_ReturnsFalse(string connectionString)
    {
        IConfiguration configuration = BuildConfiguration(connectionString);

        configuration.IsRedisConfigured().ShouldBeFalse();
    }

    [Fact]
    public void IsRedisConfigured_WhenSet_ReturnsTrue()
    {
        IConfiguration configuration = BuildConfiguration("localhost:6379");

        configuration.IsRedisConfigured().ShouldBeTrue();
    }

    private static IConfiguration BuildConfiguration(string? redisConnectionString)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redisConnectionString,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }
}
