using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace BuildingBlocks.Application.Modules;

/// <summary>
///     Dynamically discovers feature modules in the runtime context.
/// </summary>
public static class AppModuleLoader
{
    private static readonly string[] RecognizedModulePrefixes =
    [
        "Identity",
        "ProductCatalog",
        "Reviews",
        "Notifications",
        "FileStorage",
        "BackgroundJobs",
        "Webhooks",
        "Chatting",
    ];

    public static IReadOnlyList<IAppModule> Discover()
    {
        DependencyContext context =
            DependencyContext.Default
            ?? throw new InvalidOperationException(
                "DependencyContext.Default is null. Module auto-discovery requires a runtime with a populated dependency context."
            );

        List<IAppModule> modules = context
            .RuntimeLibraries.Where(l =>
                RecognizedModulePrefixes.Any(p =>
                    l.Name.Equals(p, StringComparison.OrdinalIgnoreCase)
                    || l.Name.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(l => Assembly.Load(new AssemblyName(l.Name)))
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null).Select(t => t!);
                }
            })
            .Where(t =>
                t != null
                && typeof(IAppModule).IsAssignableFrom(t)
                && t is { IsClass: true, IsAbstract: false }
            )
            .Select(t => CreateModuleInstance(t!))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        return modules;
    }

    private static IAppModule CreateModuleInstance(Type moduleType)
    {
        ConstructorInfo? ctor = moduleType.GetConstructor(Type.EmptyTypes);
        if (ctor is null)
        {
            throw new InvalidOperationException(
                $"Module '{moduleType.FullName}' must declare a public parameterless constructor for auto-discovery."
            );
        }

        return (IAppModule)ctor.Invoke(null);
    }
}
