using Identity.Application.Features.TenantInvitation.DTOs;
using SharedKernel.Application.Validation;

namespace Identity.Application.Features.TenantInvitation.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateTenantInvitationRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateTenantInvitationRequestValidator
    : DataAnnotationsValidator<CreateTenantInvitationRequest>;
