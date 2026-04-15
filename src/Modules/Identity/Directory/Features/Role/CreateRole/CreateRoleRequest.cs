using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    [NotEmpty] [MaxLength(100)] string Name,
    [Required]
    [NoWhitespaceItems]
    [MaxLengthItems(100)]
        List<string> Permissions,
    Guid? TenantId = null
);
