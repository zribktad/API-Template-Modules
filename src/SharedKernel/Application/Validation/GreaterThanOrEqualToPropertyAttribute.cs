using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propertyCache = new();

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        PropertyInfo? otherProperty = _propertyCache.GetOrAdd(
            (validationContext.ObjectType, otherPropertyName),
            static key => key.Item1.GetProperty(key.Item2)
        );
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

        if (comparableValue.GetType() != comparableOtherValue.GetType())
        {

            return new ValidationResult(
                $"Cannot compare '{validationContext.MemberName}' to '{otherPropertyName}': type mismatch.",
                [validationContext.MemberName!]
            );
        }


        if (comparableValue.CompareTo(comparableOtherValue) >= 0)
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessageString, [validationContext.MemberName!]);
    }
}
