using BuildingBlocks.Application.Context;

namespace APITemplate.Tests.Unit.FileStorage;

/// <summary>
///     Test <see cref="ITenantProvider" /> whose tenant can be set per-test, so the FileStorage global tenant
///     query filter (applied by <c>FileStorageDbContext</c>) resolves to a real tenant instead of the no-op
///     design-time provider's empty tenant (which collapses every filtered query to <c>WHERE false</c>).
/// </summary>
internal sealed class MutableTenantProvider : ITenantProvider
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public bool HasTenant => TenantId != Guid.Empty;
}
