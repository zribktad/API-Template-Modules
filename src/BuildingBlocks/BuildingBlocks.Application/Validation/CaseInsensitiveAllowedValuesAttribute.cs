using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Validates that a string value matches one of the allowed values using a case-insensitive comparison.
///     <see langword="null" /> is always accepted; use <see cref="RequiredAttribute" /> in combination
///     when a non-null value is required.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CaseInsensitiveAllowedValuesAttribute : ValidationAttribute
{
    private readonly string?[] _allowedValues;

    public CaseInsensitiveAllowedValuesAttribute(params string?[] allowedValues)
        : base("'{0}' must be one of: {1}.")
    {
        _allowedValues = allowedValues;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        string? input = value as string;

        bool isValid = _allowedValues.Any(
            allowed => string.Equals(allowed, input, StringComparison.OrdinalIgnoreCase)
        );

        if (isValid)
            return ValidationResult.Success;

        string allowed = string.Join(", ", _allowedValues.Where(v => v is not null));
        return new ValidationResult(
            string.Format(ErrorMessageString, validationContext.DisplayName, allowed),
            [validationContext.MemberName!]
        );
    }
}

