using Ardalis.Specification;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Checks whether a pending invitation exists for the given normalised email.
///     Expects a pre-normalised value; used to block duplicate invitations before creating a new one.
/// </summary>
public sealed class PendingInvitationByNormalizedEmailSpecification
    : Specification<TenantInvitationEntity>
{
    public PendingInvitationByNormalizedEmailSpecification(string normalizedEmail)
    {
        Query
            .Where(i =>
                i.Email.Normalized == normalizedEmail && i.Status == InvitationStatus.Pending
            )
            .AsNoTracking();
    }
}
