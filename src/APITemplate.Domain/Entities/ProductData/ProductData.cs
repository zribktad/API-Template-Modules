using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities.ProductData;

/// <summary>
/// Abstract base document stored in MongoDB that describes rich media associated with products.
/// Serves as the discriminator root for the <see cref="ImageProductData"/> and <see cref="VideoProductData"/> subtypes.
/// </summary>
[BsonDiscriminator(RootClass = true)]
[BsonKnownTypes(typeof(ImageProductData), typeof(VideoProductData))]
public abstract class ProductData : IHasId
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }
}
