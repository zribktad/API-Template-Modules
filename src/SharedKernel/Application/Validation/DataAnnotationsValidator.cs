using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
                    List<ValidationResult> results = new();
                    Validator.TryValidateObject(model, new ValidationContext(model), results, true);

                    // For records, also validate constructor parameter attributes that may not be on properties.
                    ValidateConstructorParameterAttributes(model, results);

                    foreach (ValidationResult result in results)
                    {
                        context.AddFailure(
                            result.MemberNames.FirstOrDefault() ?? string.Empty,
                            result.ErrorMessage!
                        );
                    }
                }
            );
    }

    /// <summary>
    ///     Inspects the first public constructor of <paramref name="model" /> and runs any
    ///     <see cref="ValidationAttribute" /> instances found on its parameters, appending
    ///     failures to <paramref name="results" />. Skips parameters whose member names already have failures.
    /// </summary>
    private static void ValidateConstructorParameterAttributes(
        T model,
        List<ValidationResult> results
    )
    {
        Type type = model.GetType();
        ConstructorInfo? constructor = type.GetConstructors().FirstOrDefault();
        if (constructor is null)
            return;

        HashSet<string> existingMembers = new(results.SelectMany(r => r.MemberNames));

        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            if (existingMembers.Contains(parameter.Name ?? string.Empty))
                continue;

            IEnumerable<ValidationAttribute> validationAttributes =
                parameter.GetCustomAttributes<ValidationAttribute>();
            PropertyInfo? property = type.GetProperty(
                parameter.Name!,
                BindingFlags.Public | BindingFlags.Instance
            );
            if (property is null)
                continue;

            object? value = property.GetValue(model);
            ValidationContext validationContext = new(model) { MemberName = parameter.Name };

            foreach (ValidationAttribute attribute in validationAttributes)
            {
                ValidationResult? result = attribute.GetValidationResult(value, validationContext);
                if (result != ValidationResult.Success && result is not null)
                    results.Add(result);
            }
        }
    }
}
