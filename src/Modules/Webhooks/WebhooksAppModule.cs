using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Webhooks;

public sealed class WebhooksAppModule : IAppModule
{
    public string Name => "Webhooks";

    public IEnumerable<Assembly> Assemblies => [typeof(WebhooksAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebhooksModule(configuration);
    }
}
