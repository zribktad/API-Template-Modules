using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

public sealed class HealthCheckModuleExtensionsTests
{
    [Fact]
    public void AddModuleHealthChecks_WithTypeList_InstantiatesModuleAndRegistersHealthChecks()
    {
        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>("FakeCheckName", "injected-check"),
            ])
            .Build();

        services.AddModuleHealthChecks(config, [typeof(FakeHealthCheckModule)]);

        ServiceProvider sp = services.BuildServiceProvider();
        ICollection<HealthCheckRegistration> registrations = sp.GetRequiredService<
            IOptions<HealthCheckServiceOptions>
        >().Value.Registrations;

        registrations.ShouldContain(r => r.Name == "injected-check");
    }

    [Fact]
    public void AddModuleHealthChecks_WithMultipleTypes_RegistersAllModules()
    {
        IServiceCollection services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddModuleHealthChecks(
            config,
            [typeof(FakeHealthCheckModule), typeof(AnotherFakeHealthCheckModule)]
        );

        ServiceProvider sp = services.BuildServiceProvider();
        ICollection<HealthCheckRegistration> registrations = sp.GetRequiredService<
            IOptions<HealthCheckServiceOptions>
        >().Value.Registrations;

        registrations.ShouldContain(r => r.Name == "fake-default");
        registrations.ShouldContain(r => r.Name == "another-fake");
    }

    // Stub modules used only by these tests
    private sealed class FakeHealthCheckModule : IHealthCheckModule
    {
        private readonly string _checkName;

        public FakeHealthCheckModule(IConfiguration configuration)
        {
            _checkName = configuration["FakeCheckName"] ?? "fake-default";
        }

        public void RegisterHealthChecks(IHealthChecksBuilder builder)
        {
            builder.AddCheck(_checkName, () => HealthCheckResult.Healthy());
        }
    }

    private sealed class AnotherFakeHealthCheckModule : IHealthCheckModule
    {
        public AnotherFakeHealthCheckModule(IConfiguration _) { }

        public void RegisterHealthChecks(IHealthChecksBuilder builder)
        {
            builder.AddCheck("another-fake", () => HealthCheckResult.Healthy());
        }
    }
}
