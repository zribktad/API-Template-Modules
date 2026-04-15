using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Validation;

public static class BoundaryValidation
{
    public static IEnumerable<ValidationResult> ValidateSort(
        string? sortBy,
        string? sortDirection,
        IReadOnlyCollection<string> allowedSortFields,
        string sortByMemberName = "SortBy",
        string sortDirectionMemberName = "SortDirection"
    )
    {
        if (
            sortBy is not null
            && !allowedSortFields.Any(field => field.Equals(sortBy, StringComparison.OrdinalIgnoreCase))
        )
        {
            yield return new ValidationResult(
                $"SortBy must be one of: {string.Join(", ", allowedSortFields)}.",
                [sortByMemberName]
            );
        }

        if (
            sortDirection is not null
            && !sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            && !sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
        )
        {
            yield return new ValidationResult(
                "SortDirection must be one of: asc, desc.",
                [sortDirectionMemberName]
            );
        }
    }

    public static ValidationResult? ValidateDateRange(
        DateTime? createdFrom,
        DateTime? createdTo,
        string createdToMemberName = "CreatedTo"
    )
    {
        if (createdFrom.HasValue && createdTo.HasValue && createdTo.Value < createdFrom.Value)
        {
            return new ValidationResult(
                "CreatedTo must be greater than or equal to CreatedFrom.",
                [createdToMemberName]
            );
        }

        return ValidationResult.Success;
    }
}
