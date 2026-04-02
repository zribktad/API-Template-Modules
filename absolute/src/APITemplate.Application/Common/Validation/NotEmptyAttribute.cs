using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Validation;

/// <summary>
/// Data annotation attribute that rejects <see langword="null"/>, whitespace strings, and
/// <see cref="Guid.Empty"/> values. Applicable to properties and constructor parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public NotEmptyAttribute()
        : base("'{0}' is required and must not be empty, whitespace, or Guid.Empty.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var isEmpty =
            value is null
            || (value is string str && string.IsNullOrWhiteSpace(str))
            || (value is Guid guid && guid == Guid.Empty);

        if (isEmpty)
            return new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                [validationContext.MemberName!]
            );

        return ValidationResult.Success;
    }
}
