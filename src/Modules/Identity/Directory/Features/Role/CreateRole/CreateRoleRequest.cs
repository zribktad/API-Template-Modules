using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Validation;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    [NotEmpty] [MaxLength(CustomRole.NameMaxLength)] string Name,
    [Required]
    [NoWhitespaceItems]
    [MaxLengthItems(CustomRole.PermissionMaxLength)]
        List<string> Permissions,
    Guid? TenantId = null
);
