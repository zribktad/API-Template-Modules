using Identity.Directory.Entities;
using ProductCatalog.Entities;
using ProductCatalog.Entities.ProductData;
using ProductCatalog.ValueObjects;
using Reviews.Domain;

namespace APITemplate.Tests.Unit.Infrastructure;

internal static class DomainTestDataFactory
{
    public static Category Category(
        Guid? id = null,
        string name = "Category",
        string? description = "Description",
        Guid? tenantId = null
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = description,
            TenantId = tenantId ?? Guid.NewGuid(),
        };

    public static Product Product(
        Guid? id = null,
        string name = "Product",
        decimal price = 10m,
        Guid? tenantId = null,
        Guid? categoryId = null
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Price = Price.FromPersistence(price),
            TenantId = tenantId ?? Guid.NewGuid(),
            CategoryId = categoryId,
        };

    public static ImageProductData ImageProductData(
        Guid? id = null,
        Guid? tenantId = null,
        string title = "Image",
        long fileSizeBytes = 42
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            Title = title,
            Format = "png",
            Width = 100,
            Height = 100,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = DateTime.UtcNow,
        };

    public static VideoProductData VideoProductData(
        Guid? id = null,
        Guid? tenantId = null,
        string title = "Video",
        long fileSizeBytes = 512
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            Title = title,
            Format = "mp4",
            Resolution = "1920x1080",
            DurationSeconds = 60,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = DateTime.UtcNow,
        };

    public static ProductReview ProductReview(
        Guid? id = null,
        Guid? productId = null,
        Guid? userId = null,
        Guid? tenantId = null,
        int rating = 5,
        string? comment = "Great"
    )
    {
        ProductReview review = global::Reviews.Domain.ProductReview.Create(
            productId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            Rating.Create(rating).Value,
            comment
        );
        review.Id = id ?? Guid.NewGuid();
        review.TenantId = tenantId ?? Guid.NewGuid();
        return review;
    }

    public static TenantInvitation TenantInvitation(
        string email = "user@example.com",
        string tokenHash = "token-hash",
        int expiryHours = 24,
        DateTimeOffset? now = null
    ) =>
        global::Identity.Directory.Entities.TenantInvitation.Create(
            email,
            tokenHash,
            expiryHours,
            new FrozenTimeProvider(now ?? DateTimeOffset.UtcNow)
        );

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
