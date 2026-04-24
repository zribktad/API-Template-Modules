using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Subscribes to the Redis pub/sub channel that carries BFF session revocation events and
///     invalidates the local cache for each received session id. Relies on StackExchange.Redis auto-
///     reconnect for transient Redis failures; on message-level errors we log and continue.
/// </summary>
public sealed class BffSessionRevocationSubscriber : IHostedService
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IBffLocalSessionCache _localCache;
    private readonly ILogger<BffSessionRevocationSubscriber> _logger;

    public BffSessionRevocationSubscriber(
        IConnectionMultiplexer multiplexer,
        IBffLocalSessionCache localCache,
        ILogger<BffSessionRevocationSubscriber> logger
    )
    {
        _multiplexer = multiplexer;
        _localCache = localCache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.SubscribeAsync(
                RedisBffSessionRevocationNotifier.Channel,
                HandleMessage
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to subscribe to BFF session revocation channel {Channel}",
                RedisBffSessionRevocationNotifier.ChannelName
            );
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.UnsubscribeAsync(RedisBffSessionRevocationNotifier.Channel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to unsubscribe from BFF session revocation channel {Channel}",
                RedisBffSessionRevocationNotifier.ChannelName
            );
        }
    }

    private void HandleMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue)
                return;

            string? sessionId = message.ToString();
            if (string.IsNullOrEmpty(sessionId))
                return;

            _localCache.Invalidate(sessionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to process BFF session revocation message on channel {Channel}",
                channel.ToString()
            );
        }
    }
}
