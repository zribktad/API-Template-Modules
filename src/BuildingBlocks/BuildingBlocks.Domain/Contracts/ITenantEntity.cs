namespace BuildingBlocks.Domain.Entities.Contracts;

/// <summary>
///     Marks a domain entity as belonging to a specific tenant, enabling query-level tenant isolation
///     via global EF Core query filters.
/// </summary>
public interface ITenantEntity
{
    public Guid TenantId { get; set; }
}

