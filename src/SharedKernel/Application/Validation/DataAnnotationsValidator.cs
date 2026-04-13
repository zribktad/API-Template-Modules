using System.ComponentModel.DataAnnotations;
using System.Linq;
using FluentValidation;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Base FluentValidation validator that bridges Data Annotations attributes into the FluentValidation
///     pipeline. Validates both property-level and constructor-parameter-level attributes, making it suitable
///     for records whose validation attributes are declared on primary constructor parameters.
/// </summary>
public abstract class DataAnnotationsValidator<T> : AbstractValidator<T>
    where T : class
{
    protected DataAnnotationsValidator()
    {
        RuleFor(x => x)
            .Custom(
                static (model, context) =>
                {
                    foreach (ValidationResult result in AttributedModelValidator.Validate(model))
                    {
                        context.AddFailure(
                            result.MemberNames.FirstOrDefault() ?? string.Empty,
                            result.ErrorMessage!
                        );
                    }
                }
            );
    }
}
