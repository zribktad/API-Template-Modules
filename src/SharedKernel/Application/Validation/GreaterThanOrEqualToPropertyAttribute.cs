using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Validates that the annotated comparable value is greater than or equal to another comparable property when both
///     are present.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class GreaterThanOrEqualToPropertyAttribute(string otherPropertyName)
    : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        PropertyInfo? otherProperty = validationContext.ObjectType.GetProperty(otherPropertyName);
        if (otherProperty is null)
        {
            throw new ValidationException(
                $"{nameof(GreaterThanOrEqualToPropertyAttribute)} could not find property '{otherPropertyName}'."
            );
        }

        object? otherValue = otherProperty.GetValue(validationContext.ObjectInstance);
        if (otherValue is null)
            return ValidationResult.Success;

        if (value is not IComparable comparableValue || otherValue is not IComparable comparableOtherValue)
        {
            throw new ValidationException(
                $"{nameof(GreaterThanOrEqualToPropertyAttribute)} can only be used on comparable values."
            );
        }

        if (comparableValue.CompareTo(comparableOtherValue) >= 0)
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessageString, [validationContext.MemberName!]);
    }
}
