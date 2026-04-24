using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Publishes revocation events over a single Redis pub/sub broadcast channel. Subscribers on
///     every instance listen to this channel and evict their local session caches; using a single
///     channel keeps the subscriber count O(1) per instance regardless of active session count.
/// </summary>
public sealed class RedisBffSessionRevocationNotifier : IBffSessionRevocationNotifier
{
    internal const string ChannelName = "bff:session:revocations";

    internal static readonly RedisChannel Channel = RedisChannel.Literal(ChannelName);

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisBffSessionRevocationNotifier> _logger;

    public RedisBffSessionRevocationNotifier(
        IConnectionMultiplexer multiplexer,
        ILogger<RedisBffSessionRevocationNotifier> logger
    )
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public async Task PublishRevokedAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_multiplexer.IsConnected)
        {
            _logger.LogDebug(
                "Skipping BFF session revocation publish for {SessionId}: Redis multiplexer not connected",
                sessionId
            );
            return;
        }

        try
        {
            ISubscriber subscriber = _multiplexer.GetSubscriber();
            await subscriber.PublishAsync(Channel, sessionId);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish BFF session revocation for {SessionId}; peers will fall back to TTL eviction",
                sessionId
            );
        }
    }
}
