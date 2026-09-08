using System.Collections.Immutable;
using System.Reflection;
using BuildingBlocks.Application.Modules;

namespace APITemplate.Api;

/// <summary>
///     Dynamic discovery of module assemblies for Wolverine handler registration.
/// </summary>
public static class WolverineModuleDiscovery
{
    /// <summary>
    ///     All module assemblies scanned for Wolverine handlers, discovered dynamically.
    /// </summary>
    public static IReadOnlyList<Assembly> HandlerAssemblies { get; } = BuildHandlerAssemblies();

    private static ImmutableArray<Assembly> BuildHandlerAssemblies()
    {
        IReadOnlyList<IAppModule> modules = AppModuleLoader.Discover();
        return modules.SelectMany(m => m.Assemblies).Distinct().ToImmutableArray();
    }
}
