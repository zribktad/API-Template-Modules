using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileStorage;

public sealed class FileStorageAppModule : IAppModule
{
    public string Name => "FileStorage";

    public IEnumerable<Assembly> Assemblies => [typeof(FileStorageAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFileStorageModule(configuration);
    }
}
