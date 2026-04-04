using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain.Entities;
using SharedKernel.Domain.Entities.Contracts;

namespace SharedKernel.Infrastructure.Configurations;

/// <summary>
///     Applies the standard tenant, audit, soft-delete, and optimistic-concurrency configuration
///     to any entity implementing <see cref="IAuditableTenantEntity" />.
/// </summary>
public static class TenantAuditableEntityConfigurationExtensions
{
    public static void ConfigureTenantAuditable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditableTenantEntity
    {
        builder.Property(e => e.TenantId).IsRequired();

        builder.OwnsOne(
            e => e.Audit,
            audit =>
            {
                audit
                    .Property(a => a.CreatedAtUtc)
                    .HasColumnName("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                audit
                    .Property(a => a.CreatedBy)
                    .HasColumnName("CreatedBy")
                    .IsRequired()
                    .HasDefaultValue(AuditDefaults.SystemActorId);

                audit
                    .Property(a => a.UpdatedAtUtc)
                    .HasColumnName("UpdatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                audit
                    .Property(a => a.UpdatedBy)
                    .HasColumnName("UpdatedBy")
                    .IsRequired()
                    .HasDefaultValue(AuditDefaults.SystemActorId);
            }
        );

        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.DeletedBy);

        builder
            .Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.IsDeleted });

        builder.ToTable(t =>
            t.HasCheckConstraint(
                $"CK_{builder.Metadata.GetTableName()}_SoftDeleteConsistency",
                "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
            )
        );
    }
}
