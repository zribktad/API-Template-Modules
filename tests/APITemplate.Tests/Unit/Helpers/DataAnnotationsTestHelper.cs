using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace APITemplate.Tests.Unit.Helpers;

/// <summary>
///     Shared DataAnnotations validation for tests using <see cref="AttributedModelValidator.Validate" />,
///     which covers both property attributes (<see cref="System.ComponentModel.DataAnnotations.Validator.TryValidateObject" />)
///     and constructor-parameter attributes for primary-constructor records.
/// </summary>
internal static class DataAnnotationsTestHelper
{
    internal static bool TryValidateAllProperties(
        object instance,
        out List<ValidationResult> results
    )
    {
        IReadOnlyList<ValidationResult> list = AttributedModelValidator.Validate(instance);
        results = [.. list];
        return results.Count == 0;
    }
}
