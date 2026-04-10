using Ardalis.Specification;
using InvitationEntity = Identity.Entities.TenantInvitation;

namespace Identity.Features.Tenant;

/// <summary>
///     Loads all non-deleted tenant invitations for a specific tenant, bypassing global query filters
///     so the spec works correctly in handlers that run without a tenant context.
/// </summary>
public sealed class InvitationsForTenantSoftDeleteSpecification : Specification<InvitationEntity>
{
    public InvitationsForTenantSoftDeleteSpecification(Guid tenantId)
    {
        Query
            .Where(invitation => invitation.TenantId == tenantId && !invitation.IsDeleted)
            .IgnoreQueryFilters();
    }
}
