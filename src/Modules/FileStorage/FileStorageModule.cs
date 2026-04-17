using FileStorage.Domain.Services;
using FileStorage.Features;
using FileStorage.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Startup;

namespace FileStorage;

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

        services.AddSingleton<IDatabaseStartupContributor, FileStorageDatabaseStartupContributor>();

        return services;
    }

    public static IEndpointRouteBuilder MapFileStorageEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
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
            .ForwardUnitOfWork<FileStorageDbMarker>()
            .AddRepository<IStoredFileRepository, StoredFileRepository>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddTransient<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IFileUploadWorkflow, FileUploadWorkflow>();
    }

    private static void RegisterControllers(IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly);
    }
}
