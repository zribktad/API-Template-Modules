using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Validation;

/// <summary>
///     Runs Data Annotations validation including attributes declared on primary-constructor
///     parameters of records (which <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}?, bool)" /> alone does not see).
/// </summary>
public interface IValidator
{
    IReadOnlyList<ValidationResult> Validate(object model);
}
