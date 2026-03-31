using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(t => t.Code).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.IsActive);
    }
}
