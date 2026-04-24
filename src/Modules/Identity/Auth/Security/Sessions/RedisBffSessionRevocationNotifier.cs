using Identity.Logging;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Publishes session-invalidation events over a single Redis pub/sub broadcast channel.
///     Subscribers on every instance listen to this channel and evict their local session caches;
///     using a single channel keeps the subscriber count O(1) per instance regardless of active
///     session count. The payload is a raw session id — callers with PUBLISH permission on the
///     shared Redis instance can force cache invalidations, so trust boundaries should treat write
///     access to the revocation channel as equivalent to session-store write access.
/// </summary>
public sealed class RedisBffSessionRevocationNotifier : IBffSessionRevocationNotifier
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisBffSessionRevocationNotifier> _logger;

    public RedisBffSessionRevocationNotifier(
        IConnectionMultiplexer multiplexer,
        ILogger<RedisBffSessionRevocationNotifier> logger
    )
    {
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger;
    }

    public async Task PublishRevokedAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            await _subscriber
                .PublishAsync(BffSessionCacheKeys.RevocationChannel, sessionId)
                .WaitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.BffSessionRevocationPublishFailed(ex, SafeRef(sessionId));
        }
    }

    // Log a short prefix only; full session ids are cookie-equivalent credentials and must not land
    // in structured logs un-redacted.
    private static string SafeRef(string sessionId) =>
        string.IsNullOrEmpty(sessionId)
            ? "(empty)"
            : sessionId[..Math.Min(8, sessionId.Length)] + "...";
}
