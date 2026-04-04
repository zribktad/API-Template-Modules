using System.ComponentModel.DataAnnotations;

namespace APITemplate.Tests.Unit.Helpers;

/// <summary>
///     Shared DataAnnotations validation for tests (property-level <c>Validator.TryValidateObject</c>, aligned with
///     <c>DataAnnotationsValidator&lt;T&gt;</c> in SharedKernel).
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
