using BuildingBlocks.Application.Context;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Tenant/actor doubles for Identity-only DB tests that need a stable tenant scope (HasTenant true).
/// </summary>
internal sealed class IdentityIntegrationTenantProvider(Guid tenantId) : ITenantProvider
{
    public Guid TenantId => tenantId;
    public bool HasTenant => true;
}

internal sealed class IdentityIntegrationEmptyActorProvider : IActorProvider
{
    public Guid ActorId => Guid.Empty;
}
