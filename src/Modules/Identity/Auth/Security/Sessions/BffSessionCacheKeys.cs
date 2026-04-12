namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Prefix for BFF session cache entries and related Redis keys (locks, refresh results).
/// </summary>
internal static class BffSessionCacheKeys
{
    public const string SessionKeyPrefix = "bff:session:";
}
