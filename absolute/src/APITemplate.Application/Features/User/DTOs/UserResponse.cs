using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.User.DTOs;

/// <summary>
/// Read model returned to callers after a user query or creation.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    UserRole Role,
    DateTime CreatedAtUtc
) : IHasId;
