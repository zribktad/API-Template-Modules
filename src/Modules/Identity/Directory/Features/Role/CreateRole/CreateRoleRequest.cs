using System.ComponentModel.DataAnnotations;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    [NotEmpty] [MaxLength(100)] string Name,
    [Required] List<string> Permissions,
    Guid? TenantId = null
) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Permissions is null)
            yield break;

        for (int i = 0; i < Permissions.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Permissions[i]))
            {
                yield return new ValidationResult(
                    "Permissions must not contain empty values.",
                    [$"{nameof(Permissions)}[{i}]"]
                );
                continue;
            }

            if (Permissions[i].Length > 100)
            {
                yield return new ValidationResult(
                    "Permissions entries must not exceed 100 characters.",
                    [$"{nameof(Permissions)}[{i}]"]
                );
            }
        }
    }
}
