using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Prefix for BFF session cache entries and related Redis keys (locks, refresh results) and
///     pub/sub channel names. Keeps the <c>bff:session:*</c> namespace owned in a single place.
/// </summary>
internal static class BffSessionCacheKeys
{
    public const string SessionKeyPrefix = "bff:session:";

    public const string RevocationChannelName = SessionKeyPrefix + "revocations";

    public static readonly RedisChannel RevocationChannel = RedisChannel.Literal(
        RevocationChannelName
    );

    public static string GetSessionKey(string sessionId) => SessionKeyPrefix + sessionId;
}
