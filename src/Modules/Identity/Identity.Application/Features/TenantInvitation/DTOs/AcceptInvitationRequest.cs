using SharedKernel.Application.Validation;

namespace Identity.Application.Features.TenantInvitation.DTOs;

/// <summary>
/// Represents the request payload for accepting a tenant invitation using a secure token.
/// </summary>
public sealed record AcceptInvitationRequest([NotEmpty] string Token);
