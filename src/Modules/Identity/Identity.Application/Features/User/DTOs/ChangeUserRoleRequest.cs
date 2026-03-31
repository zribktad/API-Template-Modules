using Identity.Domain.Enums;

namespace Identity.Application.Features.User.DTOs;

/// <summary>
/// Represents the request payload for changing a user's role.
/// </summary>
public sealed record ChangeUserRoleRequest(UserRole Role);
