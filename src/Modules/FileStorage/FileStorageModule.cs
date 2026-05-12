using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options;
using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Infrastructure.EFCore.Registration;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using BuildingBlocks.Web.Configuration;
using FileStorage.Domain.Services;
using FileStorage.Domain.Storage;
using FileStorage.Features;
using FileStorage.Persistence;
using FileStorage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

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

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleOptions<FileStorageOptions>(configuration);

        // Normalize extensions once so call sites can do straightforward ordinal lookups regardless
        // of the casing supplied in configuration (e.g. ".PNG" vs ".png").
        services.PostConfigure<FileStorageOptions>(opts =>
        {
            opts.AllowedExtensions = opts
                .AllowedExtensions.Select(e => e.ToLowerInvariant())
                .ToArray();
        });
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
        // Blob store backends — today just "local"; future backends register additional KeyedBlobStore entries.
        services.AddScoped<LocalBlobStore>();
        services.AddScoped(sp => new KeyedBlobStore(
            "local",
            sp.GetRequiredService<LocalBlobStore>()
        ));
        services.AddScoped<IBlobStoreFactory, BlobStoreFactory>();

        services.AddScoped<IOrphanBlobSweepService, OrphanBlobSweepService>();

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.FileStorageDelete,
            builder =>
            {
                builder.AddRetry(
                    new RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(200),
                        UseJitter = true,
                    }
                );
            }
        );
        services.AddSingleton<
            IFileStorageDeletePipelineProvider,
            FileStorageDeletePipelineProvider
        >();
    }

    private static void RegisterControllers(IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly);
    }
}
