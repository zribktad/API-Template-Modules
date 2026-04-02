using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="TenantInvitation"/> with token hash and pending-invitation lookup methods.
/// </summary>
public sealed class TenantInvitationRepository
    : RepositoryBase<TenantInvitation>,
        ITenantInvitationRepository
{
    private readonly IdentityDbContext _db;

    public TenantInvitationRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _db = dbContext;
    }

    public Task<TenantInvitation?> GetValidByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    ) =>
        _db.TenantInvitations.FirstOrDefaultAsync(
            i => i.TokenHash == tokenHash && i.Status != InvitationStatus.Revoked,
            ct
        );

    public Task<bool> HasPendingInvitationAsync(
        string normalizedEmail,
        CancellationToken ct = default
    ) =>
        _db
            .TenantInvitations.AsNoTracking()
            .AnyAsync(
                i => i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending,
                ct
            );
}
