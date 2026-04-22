using System.ComponentModel.DataAnnotations;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.TenantInvitation.DTOs;

/// <summary>
///     Represents the request payload for inviting a user to the current tenant by email address.
/// </summary>
public sealed record CreateTenantInvitationRequest(
    [NotEmpty] [MaxLength(AppUser.EmailMaxLength)] [EmailAddress] string Email
);
