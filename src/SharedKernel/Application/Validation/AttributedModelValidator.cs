using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Runs Data Annotations validation the same way as <see cref="DataAnnotationsValidator{T}" />: property validation
///     via <see cref="Validator.TryValidateObject" /> plus constructor-parameter attributes for primary-constructor records.
/// </summary>
public static class AttributedModelValidator
{
    private sealed record CachedParam(PropertyInfo Property, ValidationAttribute[] Attributes, string Name);

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<CachedParam>> _paramCache = new();

    private static IReadOnlyList<CachedParam> GetCachedParams(Type type) =>
        _paramCache.GetOrAdd(type, static t =>
        {
            ConstructorInfo? ctor = t.GetConstructors().FirstOrDefault();
            if (ctor is null)
                return [];

            return ctor.GetParameters()
                .Select(p => new CachedParam(
                    t.GetProperty(p.Name!, BindingFlags.Public | BindingFlags.Instance)!,
                    p.GetCustomAttributes<ValidationAttribute>().ToArray(),
                    p.Name!
                ))
                .Where(x => x.Property is not null)
                .ToArray();
        });

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
        IReadOnlyList<CachedParam> cachedParams = GetCachedParams(type);
        if (cachedParams.Count == 0)
            return;

        HashSet<string> existingMembers = new(results.SelectMany(r => r.MemberNames));

        foreach (CachedParam param in cachedParams)
        {
            if (existingMembers.Contains(param.Name))
                continue;

            object? value = param.Property.GetValue(model);
            ValidationContext validationContext = new(model) { MemberName = param.Name };

            foreach (ValidationAttribute attribute in param.Attributes)
            {
                ValidationResult? result = attribute.GetValidationResult(value, validationContext);
                if (result != ValidationResult.Success && result is not null)
                    results.Add(result);
            }
        }
    }
}
