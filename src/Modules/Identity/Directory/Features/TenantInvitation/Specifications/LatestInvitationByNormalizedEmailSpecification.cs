using Ardalis.Specification;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Returns the most recently created invitation for the normalised email (any status), bypassing
///     tenant filters — used to classify access denial when no local user exists.
/// </summary>
public sealed class LatestInvitationByNormalizedEmailSpecification
    : Specification<TenantInvitationEntity>
{
    public LatestInvitationByNormalizedEmailSpecification(string normalizedEmail)
    {
        Query
            .IgnoreQueryFilters()
            .Where(i => i.DbNormalizedEmail == normalizedEmail)
            .OrderByDescending(i => i.Audit.CreatedAtUtc)
            .Take(1)
            .AsNoTracking();
    }
}
