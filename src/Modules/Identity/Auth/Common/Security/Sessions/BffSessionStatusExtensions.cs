namespace Identity.Auth.Security.Sessions;

internal static class BffSessionStatusExtensions
{
    /// <summary>
    ///     Returns <see langword="true" /> for statuses that represent an irreversible end state
    ///     (<see cref="BffSessionStatus.Revoked" /> or <see cref="BffSessionStatus.Expired" />).
    ///     Terminal sessions must not be refreshed and should be evicted from caches immediately.
    /// </summary>
    public static bool IsTerminal(this BffSessionStatus status) =>
        status is BffSessionStatus.Revoked or BffSessionStatus.Expired;
}
