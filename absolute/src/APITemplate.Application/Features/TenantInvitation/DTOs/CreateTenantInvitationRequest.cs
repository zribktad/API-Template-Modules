using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.TenantInvitation.DTOs;

/// <summary>
/// Represents the request payload for inviting a user to the current tenant by email address.
/// </summary>
public sealed record CreateTenantInvitationRequest(
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
