using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public const string TenantCodeIndexName = "IX_Tenants_Code";

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ConfigureTenantAuditable();

        builder
            .Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100)
            .HasConversion(tc => tc.Value, value => TenantCode.FromPersistence(value));
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(t => t.Code).HasDatabaseName(TenantCodeIndexName).IsUnique();
        builder.HasIndex(t => t.IsActive);
    }
}

