using APITemplate.Api;
using BuildingBlocks.Web.Health;
using Notifications.Infrastructure.Health;
using ProductCatalog.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Health;

[Trait("Category", "Unit")]
public sealed class HealthCheckModuleRegistryTests
{
    [Fact]
    public void Modules_AllTypesImplementIHealthCheckModule()
    {
        HealthCheckModuleRegistry.Modules.ShouldAllBe(t =>
            typeof(IHealthCheckModule).IsAssignableFrom(t)
        );
    }

    [Fact]
    public void Modules_ContainsSharedKernelHealthChecks()
    {
        HealthCheckModuleRegistry.Modules.ShouldContain(typeof(SharedKernelHealthChecks));
    }

    [Fact]
    public void Modules_ContainsProductCatalogHealthChecks()
    {
        HealthCheckModuleRegistry.Modules.ShouldContain(typeof(ProductCatalogHealthChecks));
    }

    [Fact]
    public void Modules_ContainsNotificationsHealthChecks()
    {
        HealthCheckModuleRegistry.Modules.ShouldContain(typeof(NotificationsHealthChecks));
    }
}
