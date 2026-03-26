using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.TenantInvitation.DTOs;

namespace APITemplate.Application.Features.TenantInvitation.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateTenantInvitationRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateTenantInvitationRequestValidator
    : DataAnnotationsValidator<CreateTenantInvitationRequest>;
