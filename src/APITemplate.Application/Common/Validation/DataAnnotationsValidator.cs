using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;

/// <summary>
/// Base FluentValidation validator that bridges Data Annotations attributes into the FluentValidation
/// pipeline. Validates both property-level and constructor-parameter-level attributes, making it suitable
/// for records whose validation attributes are declared on primary constructor parameters.
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
                    var results = new List<ValidationResult>();
                    Validator.TryValidateObject(
                        model,
                        new ValidationContext(model),
                        results,
                        validateAllProperties: true
                    );

                    // For records, also validate constructor parameter attributes that may not be on properties.
                    ValidateConstructorParameterAttributes(model, results);

                    foreach (var result in results)
                        context.AddFailure(
                            result.MemberNames.FirstOrDefault() ?? string.Empty,
                            result.ErrorMessage!
                        );
                }
            );
    }

    /// <summary>
    /// Inspects the first public constructor of <paramref name="model"/> and runs any
    /// <see cref="ValidationAttribute"/> instances found on its parameters, appending
    /// failures to <paramref name="results"/>. Skips parameters whose member names already have failures.
    /// </summary>
    private static void ValidateConstructorParameterAttributes(
        T model,
        List<ValidationResult> results
    )
    {
        var type = model.GetType();
        var constructor = type.GetConstructors().FirstOrDefault();
        if (constructor is null)
            return;

        var existingMembers = new HashSet<string>(results.SelectMany(r => r.MemberNames));

        foreach (var parameter in constructor.GetParameters())
        {
            if (existingMembers.Contains(parameter.Name ?? string.Empty))
                continue;

            var validationAttributes = parameter.GetCustomAttributes<ValidationAttribute>();
            var property = type.GetProperty(
                parameter.Name!,
                BindingFlags.Public | BindingFlags.Instance
            );
            if (property is null)
                continue;

            var value = property.GetValue(model);
            var validationContext = new ValidationContext(model) { MemberName = parameter.Name };

            foreach (var attribute in validationAttributes)
            {
                var result = attribute.GetValidationResult(value, validationContext);
                if (result != ValidationResult.Success && result is not null)
                    results.Add(result);
            }
        }
    }
}
