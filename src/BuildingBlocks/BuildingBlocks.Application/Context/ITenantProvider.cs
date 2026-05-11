namespace BuildingBlocks.Application.Context;

/// <summary>
///     Provides the tenant context for the current request, enabling multi-tenant data isolation
///     at the Application layer without coupling handlers to HTTP or infrastructure concerns.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Gets the unique identifier of the current tenant.</summary>
    Guid TenantId { get; }

    bool HasTenant { get; }
}
