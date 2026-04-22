using System.ComponentModel.DataAnnotations;
using Identity.ValueObjects;

namespace Identity.Directory.Features.User;

/// <summary>
///     Represents the request payload for triggering a Keycloak password-reset email for the given email address.
/// </summary>
public sealed record RequestPasswordResetRequest(
    [NotEmpty] [MaxLength(Email.MaxLength)] [EmailAddress] string Email
);
