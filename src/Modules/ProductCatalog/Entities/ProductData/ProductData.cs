using MongoDB.Bson.Serialization.Attributes;
using SharedKernel.Domain.Common;

namespace ProductCatalog.Entities.ProductData;

/// <summary>
///     Abstract base document stored in MongoDB that describes rich media associated with products.
///     Serves as the discriminator root for the <see cref="ImageProductData" /> and <see cref="VideoProductData" />
///     subtypes.
/// </summary>
[BsonDiscriminator(RootClass = true)]
[BsonKnownTypes(typeof(ImageProductData), typeof(VideoProductData))]
public abstract class ProductData : IHasId
{
    public const string CollectionName = "product_data";

    [BsonElement("tenantId")]
    public Guid TenantId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    public bool PendingDeletion { get; set; }

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();
}
