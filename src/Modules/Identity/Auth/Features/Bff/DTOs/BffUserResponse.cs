using SharedKernel.Infrastructure.Logging;

namespace Identity.Auth.Features.Bff.DTOs;

/// <summary>
///     Represents the authenticated user's identity and role information returned by the Backend-for-Frontend (BFF) user
///     endpoint.
/// </summary>
public sealed record BffUserResponse(
    string? UserId,
    string? Username,
    [property: PersonalData] string? Email,
    string? TenantId,
    string[] Roles
);
