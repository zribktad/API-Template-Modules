using BuildingBlocks.Infrastructure.EFCore.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public const string TenantCodeIndexName = "IX_Tenants_Code";

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(t => t.Code).IsRequired().HasMaxLength(Tenant.CodeMaxLength);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(Tenant.NameMaxLength);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(t => t.Code).HasDatabaseName(TenantCodeIndexName).IsUnique();
        builder.HasIndex(t => t.IsActive);

        builder
            .HasIndex(t => new { t.Code, t.Name })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("english");
    }
}
