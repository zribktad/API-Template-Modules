using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Modules;

/// <summary>
///     Contract implemented by feature modules for dynamic discovery and registration.
/// </summary>
public interface IAppModule
{
    /// <summary>
    ///     Unique name of the module (e.g., "Identity", "ProductCatalog").
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     All assemblies contributed by this module (used for Wolverine handler scanning, controllers, etc.).
    /// </summary>
    IEnumerable<Assembly> Assemblies { get; }

    /// <summary>
    ///     Registers the module's services in DI.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    ///     Optional pipeline configuration for the module (endpoints, routes).
    /// </summary>
    void ConfigureEndpoints(IEndpointRouteBuilder endpoints) { }
}
