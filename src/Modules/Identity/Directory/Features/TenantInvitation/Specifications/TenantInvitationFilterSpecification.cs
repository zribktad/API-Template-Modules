using Ardalis.Specification;
using Identity.Directory.Features.TenantInvitation.Mappings;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Retrieves tenant invitations matching the given filter, projected to <see cref="TenantInvitationResponse"/>
///     and ordered by creation date descending.
/// </summary>
public sealed class TenantInvitationFilterSpecification
    : Specification<TenantInvitationEntity, TenantInvitationResponse>
{
    public TenantInvitationFilterSpecification(TenantInvitationFilter filter)
    {
        Query.ApplyFilter(filter);
        Query.AsNoTracking();
        Query.OrderByDescending(i => i.Audit.CreatedAtUtc);
        Query.Select(TenantInvitationMappings.Projection);
    }
}

internal static class TenantInvitationFilterCriteria
{
    public static void ApplyFilter(
        this ISpecificationBuilder<TenantInvitationEntity> query,
        TenantInvitationFilter filter
    )
    {
        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string normalized = NormalizedString.Normalize(filter.Email);
            query.Where(i => i.DbNormalizedEmail.Contains(normalized));
        }

        if (filter.Status.HasValue)
            query.Where(i => i.Status == filter.Status.Value);
    }
}
