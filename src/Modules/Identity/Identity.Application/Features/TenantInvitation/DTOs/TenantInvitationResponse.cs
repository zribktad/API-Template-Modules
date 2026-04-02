using Identity.Domain.Enums;
using SharedKernel.Domain.Entities.Contracts;

namespace Identity.Application.Features.TenantInvitation.DTOs;

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
