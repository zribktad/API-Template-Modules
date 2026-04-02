using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.User.DTOs;

/// <summary>
/// Represents the request payload for triggering a Keycloak password-reset email for the given email address.
/// </summary>
public sealed record RequestPasswordResetRequest(
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
