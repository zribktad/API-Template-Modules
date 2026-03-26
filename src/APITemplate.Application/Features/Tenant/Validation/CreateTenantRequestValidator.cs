using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Tenant.DTOs;

namespace APITemplate.Application.Features.Tenant.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateTenantRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateTenantRequestValidator : DataAnnotationsValidator<CreateTenantRequest>;
