using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Validates that each string item in a collection does not exceed the configured maximum length.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxLengthItemsAttribute : ValidationAttribute
{
    public MaxLengthItemsAttribute(int maxLength)
    {
        MaxLength = maxLength;
        ErrorMessage = "{0} entries must not exceed " + maxLength + " characters.";
    }

    public int MaxLength { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not IEnumerable enumerable || value is string)
            throw new ValidationException($"{nameof(MaxLengthItemsAttribute)} can only be used on collections.");

        foreach (object? item in enumerable)
        {
            if (item is null)
                continue;

            if (item is string s && s.Length > MaxLength)
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

