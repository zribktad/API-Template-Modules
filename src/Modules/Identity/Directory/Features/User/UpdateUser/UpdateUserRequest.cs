using System.ComponentModel.DataAnnotations;
using Identity.Directory.Entities;
using Identity.ValueObjects;

namespace Identity.Directory.Features.User;

/// <summary>
///     Represents the request payload for updating an existing user's username and email.
/// </summary>
public sealed record UpdateUserRequest(
    [NotEmpty] [MaxLength(AppUser.UsernameMaxLength)] string Username,
    [NotEmpty] [MaxLength(Email.MaxLength)] [EmailAddress] string Email
);
