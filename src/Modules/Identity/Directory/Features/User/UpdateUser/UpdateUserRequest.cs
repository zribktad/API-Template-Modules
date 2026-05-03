using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Web.Logging;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.User;

/// <summary>
///     Represents the request payload for updating an existing user's username and email.
/// </summary>
public sealed record UpdateUserRequest(
    [NotEmpty] [MaxLength(AppUser.UsernameMaxLength)] string Username,
    [NotEmpty]
    [MaxLength(AppUser.EmailMaxLength)]
    [EmailAddress]
    [property: PersonalData]
        string Email
);
