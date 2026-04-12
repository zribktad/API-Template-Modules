namespace Identity.Auth.Security.Sessions;

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

    /// <summary>
    ///     Revokes every persisted BFF session whose Keycloak subject matches.
    /// </summary>
    Task RevokeAllSessionsForSubjectAsync(
        string keycloakSubject,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    );
}
