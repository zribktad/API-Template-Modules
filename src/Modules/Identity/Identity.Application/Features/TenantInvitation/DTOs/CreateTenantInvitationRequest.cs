using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Identity.Application.Features.TenantInvitation.DTOs;

/// <summary>
/// Represents the request payload for inviting a user to the current tenant by email address.
/// </summary>
public sealed record CreateTenantInvitationRequest(
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email
);
