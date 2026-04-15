using System.ComponentModel.DataAnnotations;

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
        results = [];
        return Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true
        );
    }
}
