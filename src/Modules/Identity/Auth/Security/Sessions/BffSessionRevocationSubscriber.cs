using System.Text.RegularExpressions;
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
public sealed partial class BffSessionRevocationSubscriber : IHostedService
{
    // Session ids are generated as Guid.ToString("N") — 32 lowercase hex chars. Constraining to that
    // format drops malformed or malicious payloads from untrusted publishers on the shared channel.
    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex SessionIdFormat();

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IBffLocalSessionCache _localCache;
    private readonly ILogger<BffSessionRevocationSubscriber> _logger;
    private EventHandler<ConnectionFailedEventArgs>? _reconnectHandler;

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
        _reconnectHandler = async (_, _) => await TrySubscribeAsync();
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

        try
        {
            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.UnsubscribeAsync(BffSessionCacheKeys.RevocationChannel, HandleMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.BffSessionRevocationUnsubscribeFailed(
                ex,
                BffSessionCacheKeys.RevocationChannelName
            );
        }
    }

    private async Task TrySubscribeAsync()
    {
        try
        {
            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.SubscribeAsync(BffSessionCacheKeys.RevocationChannel, HandleMessage);
        }
        catch (Exception ex)
        {
            _logger.BffSessionRevocationSubscribeFailed(
                ex,
                BffSessionCacheKeys.RevocationChannelName
            );
        }
    }

    private void HandleMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue)
                return;

            string sessionId = message.ToString();
            if (!SessionIdFormat().IsMatch(sessionId))
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
