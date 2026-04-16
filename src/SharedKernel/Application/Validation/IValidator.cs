using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Validation;

/// <summary>
///     Runs Data Annotations validation including attributes declared on primary-constructor
///     parameters of records (which <see cref="Validator.TryValidateObject" /> alone does not see).
/// </summary>
public interface IValidator
{
    IReadOnlyList<ValidationResult> Validate(object model);
}
