using BuildingBlocks.Web.Logging;

namespace Identity.Directory.Features.TenantInvitation.DTOs;

/// <summary>
///     Read model returned to callers for tenant invitation queries.
/// </summary>
public sealed record TenantInvitationResponse(
    Guid Id,
    [property: PersonalData] string Email,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc
) : IHasId;
