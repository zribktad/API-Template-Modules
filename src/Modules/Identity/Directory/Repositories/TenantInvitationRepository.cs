using Identity.Directory.Features.TenantInvitation.Specifications;

namespace Identity.Directory.Repositories;

/// <summary>
///     EF Core repository for <see cref="TenantInvitation" /> with invitation-specific lookup operations.
/// </summary>
public sealed class TenantInvitationRepository
    : RepositoryBase<TenantInvitation>,
        ITenantInvitationRepository
{
    public TenantInvitationRepository(IdentityDbContext dbContext)
        : base(dbContext) { }

    public Task<TenantInvitation?> GetNonRevokedByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    )
    {
        return FirstOrDefaultAsync(new NonRevokedInvitationByTokenHashSpecification(tokenHash), ct);
    }

    public Task<bool> HasPendingInvitationAsync(
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        return AnyAsync(new PendingInvitationByNormalizedEmailSpecification(normalizedEmail), ct);
    }
}
