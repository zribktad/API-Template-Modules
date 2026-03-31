namespace Identity.Application.Features.Bff.DTOs;

/// <summary>
/// Represents the authenticated user's identity and role information returned by the Backend-for-Frontend (BFF) user endpoint.
/// </summary>
public sealed record BffUserResponse(
    string? UserId,
    string? Username,
    string? Email,
    string? TenantId,
    string[] Roles
);
