namespace SharedKernel.Application.Context;

/// <summary>
/// Provides the tenant context for the current request, enabling multi-tenant data isolation
/// at the Application layer without coupling handlers to HTTP or infrastructure concerns.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Gets the unique identifier of the current tenant.</summary>
    Guid TenantId { get; }

    /// <summary>
    /// Returns <c>true</c> when the current request is scoped to a tenant;
    /// <c>false</c> for system-level or anonymous requests.
    /// </summary>
    bool HasTenant { get; }
}
