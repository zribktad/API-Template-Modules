namespace Identity.Features.TenantInvitation.Validation;

/// <summary>
///     FluentValidation validator for <see cref="CreateTenantInvitationRequest" /> that enforces data-annotation
///     constraints.
/// </summary>
public sealed class CreateTenantInvitationRequestValidator
    : DataAnnotationsValidator<CreateTenantInvitationRequest>;
