namespace Identity.Directory.Interfaces;

/// <summary>
///     Repository contract for <see cref="TenantInvitation" /> entities with invitation-specific lookup operations.
/// </summary>
public interface ITenantInvitationRepository : IRepository<TenantInvitation>
{
    /// <summary>
    ///     Returns the non-revoked invitation that matches the given hashed token, or <c>null</c> if none exists.
    ///     Expiry and acceptance validation is handled by the domain entity.
    /// </summary>
    public Task<TenantInvitation?> GetNonRevokedByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns <c>true</c> if there is already a pending invitation for the given normalised email address.
    /// </summary>
    public Task<bool> HasPendingInvitationAsync(
        string normalizedEmail,
        CancellationToken ct = default
    );
}
