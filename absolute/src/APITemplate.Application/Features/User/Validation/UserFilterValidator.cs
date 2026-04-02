using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Enums;
using FluentValidation;

namespace APITemplate.Application.Features.User.Validation;

/// <summary>
/// FluentValidation validator for <see cref="UserFilter"/> that composes sort-field rules and validates the optional role enum.
/// </summary>
public sealed class UserFilterValidator : DataAnnotationsValidator<UserFilter>
{
    /// <summary>
    /// Registers sort-field and optional role enum-range validation rules.
    /// </summary>
    public UserFilterValidator()
    {
        Include(new SortableFilterValidator<UserFilter>(UserSortFields.Map.AllowedNames));

        RuleFor(x => x.Role)
            .IsInEnum()
            .When(x => x.Role.HasValue)
            .WithMessage("Role must be a valid UserRole value.");
    }
}
