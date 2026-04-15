using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace APITemplate.Tests.Unit.Helpers;

/// <summary>
///     Shared DataAnnotations validation for tests using <c>Validator.TryValidateObject</c> with
///     <c>validateAllProperties: true</c>, matching the application's HTTP boundary validation behavior.
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
