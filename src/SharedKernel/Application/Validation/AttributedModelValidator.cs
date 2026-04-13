using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Runs Data Annotations validation the same way as <see cref="DataAnnotationsValidator{T}" />: property validation
///     via <see cref="Validator.TryValidateObject" /> plus constructor-parameter attributes for primary-constructor records.
/// </summary>
public static class AttributedModelValidator
{
    /// <summary>
    ///     Returns all validation failures for <paramref name="model" /> (empty if valid).
    /// </summary>
    public static IReadOnlyList<ValidationResult> Validate(object model)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true
        );
        AppendConstructorParameterAttributeResults(model, results);
        return results;
    }

    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="model" /> has no validation errors.
    /// </summary>
    public static bool IsValid(object model) => Validate(model).Count == 0;

    private static void AppendConstructorParameterAttributeResults(
        object model,
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
