using System.ComponentModel.DataAnnotations;

namespace Identity.Application.Features.Tenant.DTOs;

/// <summary>
/// Represents the request payload for creating a new tenant.
/// </summary>
public sealed record CreateTenantRequest(
    [Required, MaxLength(100)] string Code,
    [Required, MaxLength(200)] string Name
);
