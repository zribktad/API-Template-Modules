using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications;

public sealed class NotificationsAppModule : IAppModule
{
    public string Name => "Notifications";

    public IEnumerable<Assembly> Assemblies => [typeof(NotificationsAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotificationsModule(configuration);
    }
}
