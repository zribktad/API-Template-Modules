using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.Auditing;

namespace Reviews.Persistence;

public sealed class ReviewsDbContext : ModuleDbContext
{
    public ReviewsDbContext(
        DbContextOptions<ReviewsDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
