using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="TenantInvitation"/> with token hash and pending-invitation lookup methods.
/// </summary>
public sealed class TenantInvitationRepository
    : RepositoryBase<TenantInvitation>,
        ITenantInvitationRepository
{
    public TenantInvitationRepository(AppDbContext dbContext)
        : base(dbContext) { }

    /// <summary>Returns a pending invitation matching the given token hash, or <c>null</c> if none is found.</summary>
    public Task<TenantInvitation?> GetValidByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    ) =>
        AppDb.TenantInvitations.FirstOrDefaultAsync(
            i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending,
            ct
        );

    /// <summary>Returns <c>true</c> when a pending invitation already exists for the given normalized email address.</summary>
    public Task<bool> HasPendingInvitationAsync(
        string normalizedEmail,
        CancellationToken ct = default
    ) =>
        AppDb.TenantInvitations.AnyAsync(
            i => i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending,
            ct
        );
}
