using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Validates that a Guid collection does not contain <see cref="Guid.Empty" /> values.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NoEmptyGuidItemsAttribute : ValidationAttribute
{
    public NoEmptyGuidItemsAttribute()
        : base("{0} cannot contain an empty value.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not IEnumerable enumerable || value is string)
            throw new ValidationException($"{nameof(NoEmptyGuidItemsAttribute)} can only be used on collections.");

        foreach (object? item in enumerable)
        {
            if (item is Guid guid && guid == Guid.Empty)
            {
                return new ValidationResult(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ErrorMessageString,
                        validationContext.DisplayName
                    ),
                    [validationContext.MemberName!]
                );
            }
        }

        return ValidationResult.Success;
    }
}
