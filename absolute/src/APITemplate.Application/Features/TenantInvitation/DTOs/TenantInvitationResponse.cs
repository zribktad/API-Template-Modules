using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.TenantInvitation.DTOs;

/// <summary>
/// Read model returned to callers for tenant invitation queries.
/// </summary>
public sealed record TenantInvitationResponse(
    Guid Id,
    string Email,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc
) : IHasId;
