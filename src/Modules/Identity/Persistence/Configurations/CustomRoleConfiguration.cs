using Identity.Directory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain.Entities;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Persistence.Configurations;

public sealed class CustomRoleConfiguration : IEntityTypeConfiguration<CustomRole>
{
    public void Configure(EntityTypeBuilder<CustomRole> builder)
    {
        builder.HasKey(r => r.Id);

        // Custom Auditable and Soft Delete configuration (since it's not a strict IAuditableTenantEntity)
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

        builder.ConfigurePostgresXminConcurrency();

        builder.HasIndex(r => r.TenantId);

        builder.ToTable(t =>
            t.HasCheckConstraint(
                $"CK_{builder.Metadata.GetTableName()}_SoftDeleteConsistency",
                "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
            )
        );

        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);

        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        builder.OwnsMany(
            r => r.Permissions,
            permissions =>
            {
                permissions.ToTable("RolePermissions", "identity");
                permissions.WithOwner(rp => rp.Role).HasForeignKey(rp => rp.RoleId);
                permissions.Property(rp => rp.Permission).IsRequired().HasMaxLength(100);
                permissions.HasKey(rp => new { rp.RoleId, rp.Permission });
            }
        );
    }
}
