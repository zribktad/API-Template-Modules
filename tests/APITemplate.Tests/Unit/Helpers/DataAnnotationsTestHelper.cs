using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace APITemplate.Tests.Unit.Helpers;

/// <summary>
///     Data Annotations validation aligned with <see cref="AttributedModelValidator" /> / <see cref="DataAnnotationsValidator{T}" />.
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
