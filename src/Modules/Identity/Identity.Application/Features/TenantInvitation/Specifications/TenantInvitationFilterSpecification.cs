using Identity.Application.Features.TenantInvitation.DTOs;
using Identity.Application.Features.TenantInvitation.Mappings;
using Identity.Domain.Entities;
using Ardalis.Specification;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation.Specifications;

/// <summary>
/// Ardalis specification that retrieves a filtered list of tenant invitations projected to <see cref="TenantInvitationResponse"/>.
/// </summary>
public sealed class TenantInvitationFilterSpecification
    : Specification<TenantInvitationEntity, TenantInvitationResponse>
{
    /// <summary>
    /// Initialises the specification by applying filter criteria, descending creation-date ordering, and projection.
    /// </summary>
    public TenantInvitationFilterSpecification(TenantInvitationFilter filter)
    {
        Query.ApplyFilter(filter);
        Query.AsNoTracking();
        Query.OrderByDescending(i => i.Audit.CreatedAtUtc);
        Query.Select(TenantInvitationMappings.Projection);
    }
}

/// <summary>
/// Internal extension that applies shared <see cref="TenantInvitationFilter"/> criteria to an Ardalis specification builder.
/// </summary>
internal static class TenantInvitationFilterCriteria
{
    /// <summary>
    /// Adds optional email (normalised, case-insensitive contains) and status equality predicates to the query.
    /// </summary>
    public static void ApplyFilter(
        this ISpecificationBuilder<TenantInvitationEntity> query,
        TenantInvitationFilter filter
    )
    {
        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            var normalized = AppUser.NormalizeEmail(filter.Email);
            query.Where(i => i.Email.ToUpper().Contains(normalized));
        }

        if (filter.Status.HasValue)
            query.Where(i => i.Status == filter.Status.Value);
    }
}
