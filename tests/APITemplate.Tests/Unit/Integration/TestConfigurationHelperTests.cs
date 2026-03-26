using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Integration;

public sealed class TestConfigurationHelperTests
{
    [Fact]
    public void GetBaseConfiguration_DisablesTickerQBackgroundJobs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(TestConfigurationHelper.GetBaseConfiguration())
            .Build();

        configuration["BackgroundJobs:TickerQ:Enabled"].ShouldBe("false");
        configuration["Dragonfly:ConnectionString"].ShouldBe(string.Empty);
    }
}
