using System.ComponentModel.DataAnnotations;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    [NotEmpty] [MaxLength(100)] string Name,
    [Required] List<string> Permissions,
    Guid? TenantId = null
);
