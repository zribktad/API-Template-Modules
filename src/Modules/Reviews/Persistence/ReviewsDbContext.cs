using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;

namespace Reviews.Persistence;

public sealed class ReviewsDbContext : ModuleDbContext
{
    public ReviewsDbContext(
        DbContextOptions<ReviewsDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IEntityNormalizationService entityNormalizationService,
        IAuditableEntityStateManager entityStateManager
    )
        : base(
            options,
            tenantProvider,
            actorProvider,
            timeProvider,
            entityNormalizationService,
            entityStateManager
        ) { }

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewsDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }
}
