using Ardalis.Specification;
using Identity.Directory.Features.TenantInvitation.Mappings;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.Specifications;

/// <summary>
///     Ardalis specification that retrieves a filtered list of tenant invitations projected to
///     <see cref="TenantInvitationResponse" />.
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

/// <summary>
///     Internal extension that applies shared <see cref="TenantInvitationFilter" /> criteria to an Ardalis specification
///     builder.
/// </summary>
internal static class TenantInvitationFilterCriteria
{
    /// <summary>
    ///     Adds optional email (normalised, case-insensitive contains) and status equality predicates to the query.
    /// </summary>
    public static void ApplyFilter(
        this ISpecificationBuilder<TenantInvitationEntity> query,
        TenantInvitationFilter filter
    )
    {
        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string normalized = NormalizedString.Normalize(filter.Email);
            query.Where(i => i.Email.Normalized.Contains(normalized));
        }

        if (filter.Status.HasValue)
            query.Where(i => i.Status == filter.Status.Value);
    }
}
