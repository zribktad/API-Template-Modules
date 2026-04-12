using Ardalis.Specification;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Ardalis specification that finds an accepted invitation by normalised email, bypassing global
///     query filters when no tenant context is active (e.g. first-login provisioning).
/// </summary>
public sealed class AcceptedInvitationByNormalizedEmailSpecification
    : Specification<TenantInvitationEntity>
{
    public AcceptedInvitationByNormalizedEmailSpecification(string normalizedEmail)
    {
        Query
            .IgnoreQueryFilters()
            .Where(i =>
                i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Accepted
            )
            .AsNoTracking();
    }
}
