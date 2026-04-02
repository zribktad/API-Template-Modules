using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities.ProductData;

/// <summary>
/// MongoDB document subtype that represents video media linked to a product, storing video-specific metadata such as duration and resolution.
/// </summary>
[BsonDiscriminator("video")]
public sealed class VideoProductData : ProductData
{
    public int DurationSeconds { get; set; }

    public string Resolution { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
