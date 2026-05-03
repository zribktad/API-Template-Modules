using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Validates that a string collection does not contain empty or whitespace-only values.
///     By default, <see langword="null" /> items are also rejected; set <see cref="AllowNullItems" />
///     to <see langword="true" /> to skip <see langword="null" /> items instead.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NoWhitespaceItemsAttribute : ValidationAttribute
{
    public NoWhitespaceItemsAttribute()
        : base("{0} must not contain null or empty values.") { }

    /// <summary>
    ///     When <see langword="true" />, <see langword="null" /> items in the collection are skipped
    ///     rather than treated as invalid. Default is <see langword="false" />.
    /// </summary>
    public bool AllowNullItems { get; init; } = false;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not IEnumerable enumerable || value is string)
            throw new ValidationException($"{nameof(NoWhitespaceItemsAttribute)} can only be used on collections.");

        foreach (object? item in enumerable)
        {
            bool invalid = item is null ? !AllowNullItems : item is string s && string.IsNullOrWhiteSpace(s);
            if (invalid)
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

