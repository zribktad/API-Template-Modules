using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Requires a non-empty string value when another decimal property exceeds a configured threshold.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredWhenDecimalPropertyExceedsAttribute(
    string otherPropertyName,
    double threshold
) : ValidationAttribute
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propertyCache = new();
    private readonly decimal _threshold = (decimal)threshold;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        PropertyInfo? otherProperty = _propertyCache.GetOrAdd(
            (validationContext.ObjectType, otherPropertyName),
            static key => key.Item1.GetProperty(key.Item2)
        );
        if (otherProperty is null)
        {
            throw new ValidationException(
                $"{nameof(RequiredWhenDecimalPropertyExceedsAttribute)} could not find property '{otherPropertyName}'."
            );
        }

        object? otherValue = otherProperty.GetValue(validationContext.ObjectInstance);
        if (otherValue is not decimal decimalValue)
            return ValidationResult.Success;

        if (decimalValue <= _threshold)
            return ValidationResult.Success;

        if (value is string text && !string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessageString, [validationContext.MemberName!]);
    }
}

