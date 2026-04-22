using System.ComponentModel.DataAnnotations;
using Identity.Directory.Entities;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    [NotEmpty] [MaxLength(CustomRole.NameMaxLength)] string Name,
    [Required]
    [NoWhitespaceItems]
    [MaxLengthItems(CustomRole.PermissionMaxLength)]
        List<string> Permissions,
    Guid? TenantId = null
);
