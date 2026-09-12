using BuildingBlocks.Application.Modules;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Architecture;

[Trait("Category", "Unit")]
public class ModuleDiscoveryTests
{
    [Fact]
    public void Discover_should_find_all_feature_modules()
    {
        IReadOnlyList<IAppModule> modules = AppModuleLoader.Discover();

        modules.ShouldNotBeEmpty();
        string[] moduleNames = modules.Select(m => m.Name).OrderBy(n => n).ToArray();

        moduleNames.ShouldContain("BackgroundJobs");
        moduleNames.ShouldContain("Chatting");
        moduleNames.ShouldContain("FileStorage");
        moduleNames.ShouldContain("Identity");
        moduleNames.ShouldContain("Notifications");
        moduleNames.ShouldContain("ProductCatalog");
        moduleNames.ShouldContain("Reviews");
        moduleNames.ShouldContain("Webhooks");
    }

    [Fact]
    public void Each_module_should_contribute_at_least_one_assembly()
    {
        IReadOnlyList<IAppModule> modules = AppModuleLoader.Discover();

        foreach (IAppModule module in modules)
        {
            module.Assemblies.ShouldNotBeEmpty($"Module {module.Name} contributed no assemblies.");
        }
    }
}
