using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace APITemplate.Tests.Unit.Helpers;

/// <summary>
///     Shared DataAnnotations validation for tests using <see cref="DataAnnotationsValidator" />,
///     which covers both property attributes (<see cref="Validator.TryValidateObject" />)
///     and constructor-parameter attributes for primary-constructor records.
/// </summary>
internal static class DataAnnotationsTestHelper
{
    private static readonly IValidator _validator = new DataAnnotationsValidator();

    internal static bool TryValidateAllProperties(
        object instance,
        out List<ValidationResult> results
    )
    {
        IReadOnlyList<ValidationResult> list = _validator.Validate(instance);
        results = [.. list];
        return results.Count == 0;
    }
}
