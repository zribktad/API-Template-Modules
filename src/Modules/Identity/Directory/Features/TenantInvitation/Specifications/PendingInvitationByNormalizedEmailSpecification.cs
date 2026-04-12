using Ardalis.Specification;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Ardalis specification used to test whether a pending invitation exists for the given normalised email.
/// </summary>
public sealed class PendingInvitationByNormalizedEmailSpecification
    : Specification<TenantInvitationEntity>
{
    public PendingInvitationByNormalizedEmailSpecification(string normalizedEmail)
    {
        Query
            .Where(i =>
                i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending
            )
            .AsNoTracking();
    }
}
