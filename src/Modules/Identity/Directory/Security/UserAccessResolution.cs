using Identity.Directory.Entities;

namespace Identity.Directory.Security;

/// <summary>
///     Outcome of resolving whether a Keycloak-authenticated identity may use the application (local
///     <see cref="AppUser" /> exists or was created from an accepted invitation).
/// </summary>
public sealed record UserAccessResolution
{
    public required bool IsAllowed { get; init; }
    public AppUser? User { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static UserAccessResolution Allowed(AppUser user) =>
        new() { IsAllowed = true, User = user };

    public static UserAccessResolution Denied(string errorCode, string message) =>
        new()
        {
            IsAllowed = false,
            ErrorCode = errorCode,
            Message = message,
        };
}
