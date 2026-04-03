using FluentValidation;
using SharedKernel.Application.Contracts;

namespace SharedKernel.Application.Validation;

/// <summary>
/// FluentValidation validator that enforces date-range coherence for any filter implementing
/// <see cref="IDateRangeFilter"/>: <c>CreatedTo</c> must be greater than or equal to <c>CreatedFrom</c>
/// when both values are provided.
/// </summary>
public sealed class DateRangeFilterValidator<T> : AbstractValidator<T>
    where T : IDateRangeFilter
{
    public DateRangeFilterValidator()
    {
        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}
