using Identity.Application.Features.Tenant.DTOs;
using SharedKernel.Application.Validation;

namespace Identity.Application.Features.Tenant.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateTenantRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateTenantRequestValidator : DataAnnotationsValidator<CreateTenantRequest>;
