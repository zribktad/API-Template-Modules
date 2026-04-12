namespace Identity.Directory.Features.User;

/// <summary>
///     Represents the request payload for changing a user's role.
/// </summary>
public sealed record ChangeUserRoleRequest(UserRole Role);
