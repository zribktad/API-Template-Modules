using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundJobs;

public sealed class BackgroundJobsAppModule : IAppModule
{
    public string Name => "BackgroundJobs";

    public IEnumerable<Assembly> Assemblies => [typeof(BackgroundJobsAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddBackgroundJobsModule(configuration);
    }
}
