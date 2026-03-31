using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>
/// Extension methods that apply the standard tenant, audit, soft-delete, and optimistic-concurrency
/// column configuration to any entity implementing <see cref="IAuditableTenantEntity"/>.
/// </summary>
internal static class TenantAuditableEntityConfigurationExtensions
{
    public static void ConfigureTenantAuditable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditableTenantEntity
        => SharedKernel.Infrastructure.Configurations.TenantAuditableEntityConfigurationExtensions.ConfigureTenantAuditable(
            builder
        );
}
