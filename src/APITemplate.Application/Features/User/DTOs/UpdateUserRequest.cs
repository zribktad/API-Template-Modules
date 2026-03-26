using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.User.DTOs;

/// <summary>
/// Represents the request payload for updating an existing user's username and email.
/// </summary>
public sealed record UpdateUserRequest(
    [NotEmpty] [MaxLength(100)] string Username,
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
