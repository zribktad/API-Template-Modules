using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chatting;

public sealed class ChattingAppModule : IAppModule
{
    public string Name => "Chatting";

    public IEnumerable<Assembly> Assemblies => [typeof(ChattingAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChattingModule(configuration);
    }
}
