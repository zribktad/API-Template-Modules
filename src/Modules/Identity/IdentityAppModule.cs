using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity;

public sealed class IdentityAppModule : IAppModule
{
    public string Name => "Identity";

    public IEnumerable<Assembly> Assemblies => [typeof(IdentityAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);
    }
}
