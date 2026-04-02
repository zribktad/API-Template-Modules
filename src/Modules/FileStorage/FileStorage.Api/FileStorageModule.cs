using FileStorage.Api.Controllers.V1;
using FileStorage.Application.Contracts;
using FileStorage.Application.Features.Download;
using FileStorage.Domain;
using FileStorage.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Application.Options;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;

namespace FileStorage.Api;

public static class FileStorageModule
{
    public static IServiceCollection AddFileStorageModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        RegisterOptions(services, configuration);
        RegisterDbInfrastructure(services, configuration);
        RegisterApplicationServices(services);
        RegisterControllers(services);

        return services;
    }

    public static IEndpointRouteBuilder MapFileStorageEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapControllers();
        return endpoints;
    }

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<FileStorageOptions>(configuration);
    }

    private static void RegisterDbInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<FileStorageDbContext>(configuration)
            .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<FileStorage.Domain.FileStorageDbMarker>()
            .AddRepository<IStoredFileRepository, StoredFileRepository>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddTransient<IFileStorageService, LocalFileStorageService>();
    }

    private static void RegisterControllers(IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly);
    }
}
