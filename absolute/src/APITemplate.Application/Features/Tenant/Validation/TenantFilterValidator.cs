using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Tenant.DTOs;
using FluentValidation;

namespace APITemplate.Application.Features.Tenant.Validation;

/// <summary>
/// FluentValidation validator for <see cref="TenantFilter"/> that composes pagination and sort-field rules.
/// </summary>
public sealed class TenantFilterValidator : AbstractValidator<TenantFilter>
{
    /// <summary>
    /// Registers pagination and sortable-field validation rules by including shared sub-validators.
    /// </summary>
    public TenantFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new SortableFilterValidator<TenantFilter>(TenantSortFields.Map.AllowedNames));
    }
}
