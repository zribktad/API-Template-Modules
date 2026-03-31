using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities.ProductData;

/// <summary>
/// MongoDB document subtype that represents image media linked to a product, storing image-specific metadata such as dimensions and format.
/// </summary>
[BsonDiscriminator("image")]
public sealed class ImageProductData : ProductData
{
    public int Width { get; set; }

    public int Height { get; set; }

    public string Format { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
