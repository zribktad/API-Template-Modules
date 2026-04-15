using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Validates that a string collection does not contain null, empty, or whitespace-only values.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NoWhitespaceItemsAttribute : ValidationAttribute
{
    public NoWhitespaceItemsAttribute()
        : base("{0} must not contain empty values.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not IEnumerable enumerable || value is string)
            throw new ValidationException($"{nameof(NoWhitespaceItemsAttribute)} can only be used on collections.");

        foreach (object? item in enumerable)
        {
            if (item is string s && string.IsNullOrWhiteSpace(s))
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
