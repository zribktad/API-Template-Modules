using Identity.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Subscribes to the Redis pub/sub channel that carries BFF session revocation events and
///     invalidates the local cache for each received session id. Subscribe failures at startup do
///     not fail the host: the hosted service re-attempts the subscribe on every Redis
///     <see cref="IConnectionMultiplexer.ConnectionRestored" /> event so a transient boot-time
///     outage self-heals instead of silently disabling peer invalidation for the process lifetime.
/// </summary>
public sealed class BffSessionRevocationSubscriber : IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IBffLocalSessionCache _localCache;
    private readonly ILogger<BffSessionRevocationSubscriber> _logger;
    private readonly SemaphoreSlim _subscribeGate = new(1, 1);
    private EventHandler<ConnectionFailedEventArgs>? _reconnectHandler;
    private volatile bool _subscribed;

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
        _reconnectHandler = (_, _) => _ = TrySubscribeAsync();
        _multiplexer.ConnectionRestored += _reconnectHandler;

        await TrySubscribeAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_reconnectHandler is not null)
        {
            _multiplexer.ConnectionRestored -= _reconnectHandler;
            _reconnectHandler = null;
        }

        await _subscribeGate.WaitAsync(cancellationToken);
        try
        {
            if (!_subscribed)
                return;

            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.UnsubscribeAsync(BffSessionCacheKeys.RevocationChannel, HandleMessage);
            _subscribed = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.BffSessionRevocationUnsubscribeFailed(
                ex,
                BffSessionCacheKeys.RevocationChannelName
            );
        }
        finally
        {
            _subscribeGate.Release();
        }
    }

    private async Task TrySubscribeAsync()
    {
        if (_subscribed)
            return;

        await _subscribeGate.WaitAsync();
        try
        {
            if (_subscribed)
                return;

            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.SubscribeAsync(BffSessionCacheKeys.RevocationChannel, HandleMessage);
            _subscribed = true;
        }
        catch (Exception ex)
        {
            _logger.BffSessionRevocationSubscribeFailed(
                ex,
                BffSessionCacheKeys.RevocationChannelName
            );
        }
        finally
        {
            _subscribeGate.Release();
        }
    }

    public void Dispose()
    {
        _subscribeGate.Dispose();
    }

    private void HandleMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue)
                return;

            string sessionId = message.ToString();
            if (!BffSessionIds.IsValid(sessionId))
            {
                _logger.BffSessionRevocationPayloadMalformed(sessionId.Length, channel.ToString());
                return;
            }

            _localCache.Invalidate(sessionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.BffSessionRevocationMessageProcessingFailed(ex, channel.ToString());
        }
    }
}
