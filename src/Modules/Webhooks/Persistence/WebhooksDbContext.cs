using Microsoft.EntityFrameworkCore;
using Webhooks.Entities;

namespace Webhooks.Persistence;

public sealed class WebhooksDbContext : DbContext
{
    public WebhooksDbContext(DbContextOptions<WebhooksDbContext> options)
        : base(options) { }

    public DbSet<IncomingWebhook> IncomingWebhooks => Set<IncomingWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("webhooks");

        modelBuilder.Entity<IncomingWebhook>(b =>
        {
            b.ToTable("incoming_webhooks");
            b.HasKey(e => e.EventId);
            b.Property(e => e.EventId).HasMaxLength(128);
        });

        base.OnModelCreating(modelBuilder);
    }
}
