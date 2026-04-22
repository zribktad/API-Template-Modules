using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using TenantEntity = Identity.Directory.Entities.Tenant;

namespace Identity.Directory.Features.Tenant.DTOs;

/// <summary>
///     Represents the request payload for creating a new tenant.
/// </summary>
public sealed record CreateTenantRequest(
    [NotEmpty] [MaxLength(TenantEntity.CodeMaxLength)] string Code,
    [NotEmpty] [MaxLength(TenantEntity.NameMaxLength)] string Name
);
