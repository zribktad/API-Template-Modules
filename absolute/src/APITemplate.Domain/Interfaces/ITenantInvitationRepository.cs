using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="TenantInvitation"/> entities with invitation-specific lookup operations.
/// </summary>
public interface ITenantInvitationRepository : IRepository<TenantInvitation>
{
    /// <summary>
    /// Returns the non-expired, non-revoked invitation that matches the given hashed token, or <c>null</c> if none exists.
    /// </summary>
    Task<TenantInvitation?> GetValidByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns <c>true</c> if there is already a pending invitation for the given normalised email address.
    /// </summary>
    Task<bool> HasPendingInvitationAsync(string normalizedEmail, CancellationToken ct = default);
}
