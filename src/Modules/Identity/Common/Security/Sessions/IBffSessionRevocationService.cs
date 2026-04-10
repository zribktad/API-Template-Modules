namespace Identity.Security.Sessions;

/// <summary>
///     Revokes persisted BFF sessions and records the reason for invalidation.
/// </summary>
public interface IBffSessionRevocationService
{
    /// <summary>
    ///     Revokes the specified session if it still exists.
    /// </summary>
    Task RevokeAsync(
        string sessionId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    );
}
