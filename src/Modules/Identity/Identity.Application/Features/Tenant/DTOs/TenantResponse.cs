using SharedKernel.Domain.Entities.Contracts;

namespace Identity.Application.Features.Tenant.DTOs;

/// <summary>
/// Read model returned to callers after a tenant query or creation.
/// </summary>
public sealed record TenantResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc
) : IHasId;
