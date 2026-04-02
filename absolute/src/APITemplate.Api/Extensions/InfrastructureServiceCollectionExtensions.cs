using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using APITemplate.Infrastructure.FileStorage;
using APITemplate.Infrastructure.Idempotency;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers cross-cutting infrastructure services
/// such as file storage, idempotency store, job queue, and generic channel-queue helpers.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers local file storage options and the <see cref="LocalFileStorageService"/> implementation.</summary>
    public static IServiceCollection AddFileStorageServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<FileStorageOptions>(configuration.SectionFor<FileStorageOptions>());
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        return services;
    }

    /// <summary>
    /// Registers the idempotency store as a singleton, using a Redis-backed implementation
    /// when a <see cref="IConnectionMultiplexer"/> is available, otherwise falling back to
    /// an in-memory store suitable for single-instance deployments.
    /// </summary>
    public static IServiceCollection AddIdempotencyStore(this IServiceCollection services)
    {
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var multiplexer = sp.GetService<IConnectionMultiplexer>();
            if (multiplexer is not null)
                return new DistributedCacheIdempotencyStore(multiplexer);

            return new InMemoryIdempotencyStore(sp.GetRequiredService<TimeProvider>());
        });

        return services;
    }

    /// <summary>Registers the channel-based job queue and its background processing hosted service.</summary>
    public static IServiceCollection AddJobServices(this IServiceCollection services)
    {
        services.AddQueueWithConsumer<
            ChannelJobQueue,
            IJobQueue,
            IJobQueueReader,
            JobProcessingBackgroundService
        >();
        return services;
    }

    /// <summary>
    /// Registers a single <typeparamref name="TImpl"/> instance as a singleton and exposes it
    /// as both the producer interface <typeparamref name="TQueue"/> and the consumer interface
    /// <typeparamref name="TReader"/>, then starts <typeparamref name="TService"/> as a hosted service.
    /// </summary>
    public static IServiceCollection AddQueueWithConsumer<TImpl, TQueue, TReader, TService>(
        this IServiceCollection services
    )
        where TImpl : class, TQueue, TReader
        where TQueue : class
        where TReader : class
        where TService : class, IHostedService
    {
        services.AddSingleton<TImpl>();
        services.AddSingleton<TQueue>(sp => sp.GetRequiredService<TImpl>());
        services.AddSingleton<TReader>(sp => sp.GetRequiredService<TImpl>());
        services.AddHostedService<TService>();
        return services;
    }
}
