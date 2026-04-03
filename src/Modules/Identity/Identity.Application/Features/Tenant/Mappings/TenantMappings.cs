using System.Linq.Expressions;
using Identity.Application.Features.Tenant.DTOs;
using TenantEntity = Identity.Domain.Entities.Tenant;

namespace Identity.Application.Features.Tenant.Mappings;

/// <summary>
/// Provides LINQ-compatible projection expressions and in-process mapping helpers for <c>Tenant</c> entities.
/// </summary>
public static class TenantMappings
{
    /// <summary>
    /// Expression tree used by EF Core to project a <c>Tenant</c> entity directly to a <see cref="TenantResponse"/> in the database query.
    /// </summary>
    public static readonly Expression<Func<TenantEntity, TenantResponse>> Projection =
        tenant => new TenantResponse(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            tenant.IsActive,
            tenant.Audit.CreatedAtUtc
        );

    private static readonly Func<TenantEntity, TenantResponse> CompiledProjection =
        Projection.Compile();

    /// <summary>
    /// Maps a <c>Tenant</c> entity to a <see cref="TenantResponse"/> using the pre-compiled projection.
    /// </summary>
    public static TenantResponse ToResponse(this TenantEntity tenant) => CompiledProjection(tenant);
}
