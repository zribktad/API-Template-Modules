using System.ComponentModel.DataAnnotations;

namespace Identity.Features.User;

/// <summary>
///     Represents the request payload for creating a new user account.
/// </summary>
public sealed record CreateUserRequest(
    [NotEmpty] [MaxLength(100)] string Username,
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
