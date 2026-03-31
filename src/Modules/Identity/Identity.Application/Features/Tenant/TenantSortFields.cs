using SharedKernel.Application.Sorting;
using TenantEntity = Identity.Domain.Entities.Tenant;

namespace Identity.Application.Features.Tenant;

/// <summary>
/// Defines the sortable fields available for tenant queries and maps them to entity property expressions.
/// </summary>
public static class TenantSortFields
{
    public static readonly SortField Code = new("code");
    public static readonly SortField Name = new("name");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<TenantEntity> Map = new SortFieldMap<TenantEntity>()
        .Add(Code, t => t.Code)
        .Add(Name, t => t.Name)
        .Add(CreatedAt, t => t.Audit.CreatedAtUtc)
        .Default(t => t.Audit.CreatedAtUtc);
}
