using System.ComponentModel.DataAnnotations;
using SharedKernel.Infrastructure.Logging;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation.DTOs;

/// <summary>
///     Represents the request payload for inviting a user to the current tenant by email address.
/// </summary>
public sealed record CreateTenantInvitationRequest(
    [NotEmpty]
    [MaxLength(TenantInvitationEntity.EmailMaxLength)]
    [EmailAddress]
    [PersonalData]
        string Email
);
