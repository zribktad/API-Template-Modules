using APITemplate.Application.Common.Contracts;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;

/// <summary>
/// FluentValidation validator that ensures <c>SortBy</c> is one of a known set of allowed field names
/// and that <c>SortDirection</c> is either <c>asc</c> or <c>desc</c> (case-insensitive).
/// </summary>
public sealed class SortableFilterValidator<T> : AbstractValidator<T>
    where T : ISortableFilter
{
    public SortableFilterValidator(IReadOnlyCollection<string> allowedSortFields)
    {
        RuleFor(x => x.SortBy)
            .Must(s =>
                s is null
                || allowedSortFields.Any(f => f.Equals(s, StringComparison.OrdinalIgnoreCase))
            )
            .WithMessage($"SortBy must be one of: {string.Join(", ", allowedSortFields)}.");

        RuleFor(x => x.SortDirection)
            .Must(s =>
                s is null
                || s.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || s.Equals("desc", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("SortDirection must be one of: asc, desc.");
    }
}
