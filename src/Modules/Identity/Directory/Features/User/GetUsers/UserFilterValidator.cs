using FluentValidation;

namespace Identity.Directory.Features.User;

/// <summary>
///     FluentValidation validator for <see cref="UserFilter" /> that composes sort-field rules and validates the optional
///     role enum.
/// </summary>
public sealed class UserFilterValidator : DataAnnotationsValidator<UserFilter>
{
    /// <summary>
    ///     Registers sort-field and optional role enum-range validation rules.
    /// </summary>
    public UserFilterValidator()
    {
        Include(new SortableFilterValidator<UserFilter>(UserSortFields.Map.AllowedNames));
    }
}
