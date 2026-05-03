using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Validates an optional sort direction value. Accepts null, "asc", and "desc" case-insensitively.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SortDirectionAttribute : ValidationAttribute
{
    public SortDirectionAttribute()
        : base("SortDirection must be one of: asc, desc.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string direction)
            throw new ValidationException($"{nameof(SortDirectionAttribute)} can only be used on string values.");

        if (
            direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessageString, [validationContext.MemberName!]);
    }
}

