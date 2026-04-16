using TenantEntity = Identity.Directory.Entities.Tenant;

namespace Identity.Directory.Features.Tenant;

/// <summary>
///     Defines the sortable fields available for tenant queries and maps them to entity property expressions.
/// </summary>
public static class TenantSortFields
{
    public const string CodeToken = nameof(Code);
    public const string NameToken = nameof(Name);
    public const string CreatedAtToken = nameof(CreatedAt);

    public static readonly SortField Code = new(CodeToken);
    public static readonly SortField Name = new(NameToken);
    public static readonly SortField CreatedAt = new(CreatedAtToken);

    public static readonly SortFieldMap<TenantEntity> Map = new SortFieldMap<TenantEntity>()
        .Add(Code, t => (string)t.Code)
        .Add(Name, t => t.Name)
        .Add(CreatedAt, t => t.Audit.CreatedAtUtc)
        .Default(t => t.Audit.CreatedAtUtc);
}
