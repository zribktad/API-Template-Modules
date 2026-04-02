using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Enums;
using FluentValidation;

namespace APITemplate.Application.Features.User.Validation;

/// <summary>
/// FluentValidation validator for <see cref="ChangeUserRoleRequest"/> that ensures the role value is a valid <see cref="UserRole"/> enum member.
/// </summary>
public sealed class ChangeUserRoleRequestValidator : AbstractValidator<ChangeUserRoleRequest>
{
    /// <summary>
    /// Registers the enum-range rule for the <c>Role</c> property.
    /// </summary>
    public ChangeUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum().WithMessage("Role must be a valid UserRole value.");
    }
}
