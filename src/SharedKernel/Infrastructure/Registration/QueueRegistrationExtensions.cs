using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Infrastructure.Registration;

/// <summary>
/// Shared registration helpers for bounded channel queues plus their hosted consumers.
/// </summary>
public static class QueueRegistrationExtensions
{
    /// <summary>
    /// Registers a singleton queue implementation that is exposed as both producer and reader,
    /// then adds the background consumer service.
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
