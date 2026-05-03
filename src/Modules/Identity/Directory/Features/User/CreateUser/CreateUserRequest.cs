using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Web.Logging;
using Identity.Directory.Entities;
using Microsoft.Extensions.Validation;

namespace Identity.Directory.Features.User;

/// <summary>
///     Represents the request payload for creating a new user account.
///     Pilot type for .NET 10 <c>[ValidatableType]</c> source generator — see docs/validation.md.
/// </summary>
#pragma warning disable ASP0029 // ValidatableTypeAttribute is experimental — pilot only, see docs/validation.md
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateUserRequest(
    [NotEmpty] [MaxLength(AppUser.UsernameMaxLength)] string Username,
    [NotEmpty]
    [MaxLength(AppUser.EmailMaxLength)]
    [EmailAddress]
    [property: PersonalData]
        string Email
);
