using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.User.DTOs;

/// <summary>
/// Represents the request payload for creating a new user account.
/// </summary>
public sealed record CreateUserRequest(
    [NotEmpty] [MaxLength(100)] string Username,
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
