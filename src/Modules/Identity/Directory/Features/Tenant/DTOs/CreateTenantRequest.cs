using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Identity.Directory.Features.Tenant.DTOs;

/// <summary>
///     Represents the request payload for creating a new tenant.
/// </summary>
public sealed record CreateTenantRequest(
    [NotEmpty] [MaxLength(100)] string Code,
    [NotEmpty] [MaxLength(200)] string Name
);
