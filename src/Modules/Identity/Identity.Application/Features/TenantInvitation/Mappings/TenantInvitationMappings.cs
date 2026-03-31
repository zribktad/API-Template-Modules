using System.Linq.Expressions;
using Identity.Application.Features.TenantInvitation.DTOs;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation.Mappings;

/// <summary>
/// Provides LINQ-compatible projection expressions and in-process mapping helpers for <c>TenantInvitation</c> entities.
/// </summary>
public static class TenantInvitationMappings
{
    /// <summary>
    /// Expression tree used by EF Core to project a <c>TenantInvitation</c> entity directly to a <see cref="TenantInvitationResponse"/> in the database query.
    /// </summary>
    public static readonly Expression<
        Func<TenantInvitationEntity, TenantInvitationResponse>
    > Projection = i => new TenantInvitationResponse(
        i.Id,
        i.Email,
        i.Status,
        i.ExpiresAtUtc,
        i.Audit.CreatedAtUtc
    );

    private static readonly Func<
        TenantInvitationEntity,
        TenantInvitationResponse
    > CompiledProjection = Projection.Compile();

    /// <summary>
    /// Maps a <c>TenantInvitation</c> entity to a <see cref="TenantInvitationResponse"/> using the pre-compiled projection.
    /// </summary>
    public static TenantInvitationResponse ToResponse(this TenantInvitationEntity invitation) =>
        CompiledProjection(invitation);
}
