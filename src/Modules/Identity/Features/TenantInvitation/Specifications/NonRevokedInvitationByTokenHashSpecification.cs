using Ardalis.Specification;
using TenantInvitationEntity = Identity.Entities.TenantInvitation;

namespace Identity.Features.TenantInvitation.Specifications;

/// <summary>
///     Finds a tenant invitation by token hash when not revoked; leaves default tracking so updates apply.
/// </summary>
public sealed class NonRevokedInvitationByTokenHashSpecification
    : Specification<TenantInvitationEntity>
{
    public NonRevokedInvitationByTokenHashSpecification(string tokenHash)
    {
        Query.Where(i => i.TokenHash == tokenHash && i.Status != InvitationStatus.Revoked);
    }
}
